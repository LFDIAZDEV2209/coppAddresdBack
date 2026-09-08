# Plan de Implementación — Dashboard #5: Comunidad ADRED
## CQRS Pre-aggregation con Channel Pattern (Fase 1)

> **Contexto crítico para el agente ejecutor**: Este plan es autosuficiente. No se necesita ningún contexto externo. Leer cada sección en orden antes de escribir una sola línea de código.

---

## 0. Arquitectura de referencia — Cómo funciona el patrón en este proyecto

El proyecto ya implementa este patrón en 4 módulos (Patients, ProgramProgress, Telemedicine, HealthTests). El flujo es siempre el mismo:

```
GraphQL Mutation / Command Handler
    │
    ├─► 1. Escribe en la base de datos (EF Core → PostgreSQL OLTP)
    │
    └─► 2. Encola evento en memoria (IAsyncEnumerable<T> via Channel<T>)
                    │
                    ▼
        BackgroundService (HostedService)
            └─► Atomic UPSERT → PostgreSQL rollup table
                    INSERT ... ON CONFLICT DO UPDATE SET total_count = total_count + delta
```

**Regla crítica**: El `await metricsQueue.EnqueueAsync(...)` en el mutation handler NO bloquea al usuario. `Channel<T>.Writer.WriteAsync` retorna prácticamente de inmediato. La latencia de escritura HTTP no se ve afectada.

**Regla crítica**: Las lecturas del dashboard usan `SumAsync` sobre la tabla rollup (`community.community_daily_metrics`) primero. Si la tabla tiene cero filas (sistema recién iniciado), caen al OLTP como fallback.

---

## 1. Contexto del módulo — CoppAddresd.Community

### 1.1. Proyecto y tecnología
- **Proyecto**: `src/Services/CoppAddresd.Community/` — es un **microservicio standalone** (no parte de CoppAddresd.Api).
- **Tecnología**: HotChocolate GraphQL (no MediatR). Los handlers son métodos dentro de `CommunityMutation.cs`.
- **DbContext**: `CommunityDbContext` en `CommunityDbContext.cs` — schema `community.`
- **Migraciones**: Aisladas en `src/Services/CoppAddresd.Community/Migrations/`. Tabla de historial: `community.__EFMigrationsHistory`.
- **DI**: En el Program.cs del microservicio Community.

### 1.2. Entidades OLTP existentes relevantes
Todas están en el schema `community.` via `CommunityDbContext`:

| DbSet | Tabla OLTP | Escritura que lo genera |
|---|---|---|
| `db.Posts` | `community.posts` | `CreatePost(...)`, `CreatePollPost(...)` en `CommunityMutation.cs` |
| `db.Comments` | `community.comments` | `AddComment(...)` en `CommunityMutation.cs` |
| `db.Likes` | `community.likes` | `LikePost(...)`, `LikeComment(...)` |
| `db.Reposts` | `community.reposts` | `RepostPost(...)` |
| `db.PollVotes` | `community.poll_votes` | `VoteOnPoll(...)` |
| `db.XpEntries` | `community.xp_entries` | Automático al completar acciones |
| `db.Profiles` | `community.profiles` | `UpdateProfile(...)` — solo lectura para métricas |

### 1.3. Métricas que el dashboard ERP necesita
Según el catálogo maestro (`catalogo_maestro_dashboards_erp.md` sección 5):

| Métrica | Tipo | Dimensión |
|---|---|---|
| Miembros activos hoy | KPI | N/A |
| Nuevos posts últimos 30 días | Serie temporal | Por día |
| Nuevos comentarios últimos 30 días | Serie temporal | Por día |
| Nuevas reacciones (likes) últimos 30 días | Serie temporal | Por día |
| Posts por categoría/tipo | Distribución | `article`, `video`, `poll`, `question`, `tip` |
| Horas pico de publicación | Distribución horaria | Hora 0–23 |
| Top miembros por racha activa | Ranking | Top 5 |

---

## 2. Diseño de la tabla rollup

### 2.1. Entidad de dominio: `CommunityDailyMetric`
**Ubicación**: `src/Services/CoppAddresd.Community/Entities/CommunityDailyMetric.cs`

```csharp
namespace CoppAddresd.Community.Entities;

/// <summary>
/// Pre-aggregated daily rollup for ERP Community Dashboard analytics.
/// Keyed by (metric_date, metric_key, dimension_key).
/// GlobalId (Guid.Empty) stores multi-tenant aggregate totals.
/// </summary>
public class CommunityDailyMetric
{
    public DateOnly MetricDate { get; set; }
    public string MetricKey { get; set; } = "";       // "posts_count", "comments_count", "likes_count", "hourly_activity", "post_type"
    public string DimensionKey { get; set; } = "";    // "article", "video", "poll", "14" (hour), "total"
    public long TotalCount { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}
```

### 2.2. EF Core Configuration
**Ubicación**: `src/Services/CoppAddresd.Community/Configurations/CommunityDailyMetricConfiguration.cs`

```csharp
using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Community.Configurations;

public class CommunityDailyMetricConfiguration : IEntityTypeConfiguration<CommunityDailyMetric>
{
    public void Configure(EntityTypeBuilder<CommunityDailyMetric> builder)
    {
        builder.ToTable("community_daily_metrics", "community");

        builder.HasKey(x => new { x.MetricDate, x.MetricKey, x.DimensionKey });

        builder.Property(x => x.MetricDate).HasColumnName("metric_date");
        builder.Property(x => x.MetricKey).HasColumnName("metric_key").HasMaxLength(64).IsRequired();
        builder.Property(x => x.DimensionKey).HasColumnName("dimension_key").HasMaxLength(64).IsRequired();
        builder.Property(x => x.TotalCount).HasColumnName("total_count");
        builder.Property(x => x.LastUpdatedAt).HasColumnName("last_updated_at");

        builder.HasIndex(x => new { x.MetricKey, x.DimensionKey, x.MetricDate })
            .HasDatabaseName("ix_community_daily_metrics_key_dim_date");
    }
}
```

### 2.3. SQL del upsert atómico (referencia)
```sql
INSERT INTO community.community_daily_metrics (metric_date, metric_key, dimension_key, total_count, last_updated_at)
VALUES (@date, @metricKey, @dimensionKey, @increment, NOW())
ON CONFLICT (metric_date, metric_key, dimension_key)
DO UPDATE SET
    total_count = community.community_daily_metrics.total_count + EXCLUDED.total_count,
    last_updated_at = NOW();
```

---

## 3. Eventos de métricas

### 3.1. Interfaz marcadora e implementaciones
**Ubicación**: `src/Services/CoppAddresd.Community/Metrics/ICommunityMetricEvent.cs`

```csharp
namespace CoppAddresd.Community.Metrics;

/// <summary>Marker interface for all Community metric events.</summary>
public interface ICommunityMetricEvent;

/// <summary>Fired when a new post (any type) is created.</summary>
public sealed record PostCreatedMetricEvent(
    Guid PostId,
    DateOnly MetricDate,
    string PostType,   // "article", "video", "poll", "question", "tip" — from post.Type?.ToString() or "general"
    int HourOfDay      // DateTime.UtcNow.Hour — para horas pico
) : ICommunityMetricEvent;

/// <summary>Fired when a new comment is created on any post.</summary>
public sealed record CommentCreatedMetricEvent(
    Guid CommentId,
    DateOnly MetricDate,
    int HourOfDay
) : ICommunityMetricEvent;

/// <summary>Fired when a like is added to a post or comment.</summary>
public sealed record LikeAddedMetricEvent(
    Guid TargetId,
    DateOnly MetricDate,
    int HourOfDay
) : ICommunityMetricEvent;

/// <summary>Fired when a like is removed (decrements total).</summary>
public sealed record LikeRemovedMetricEvent(
    Guid TargetId,
    DateOnly MetricDate
) : ICommunityMetricEvent;

/// <summary>Fired when a repost is created.</summary>
public sealed record RepostCreatedMetricEvent(
    Guid RepostId,
    DateOnly MetricDate,
    int HourOfDay
) : ICommunityMetricEvent;
```

---

## 4. Cola en memoria

### 4.1. Interface
**Ubicación**: `src/Services/CoppAddresd.Community/Metrics/ICommunityMetricsQueue.cs`

```csharp
namespace CoppAddresd.Community.Metrics;

/// <summary>
/// In-memory channel queue for Community metric events.
/// Non-blocking enqueue; background worker drains the channel.
/// </summary>
public interface ICommunityMetricsQueue
{
    ValueTask EnqueueAsync(ICommunityMetricEvent metricEvent, CancellationToken ct = default);
    IAsyncEnumerable<ICommunityMetricEvent> ReadAllAsync(CancellationToken ct = default);
}
```

### 4.2. Implementación
**Ubicación**: `src/Services/CoppAddresd.Community/Metrics/CommunityMetricsQueue.cs`

```csharp
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace CoppAddresd.Community.Metrics;

public sealed class CommunityMetricsQueue : ICommunityMetricsQueue
{
    private readonly Channel<ICommunityMetricEvent> _channel =
        Channel.CreateUnbounded<ICommunityMetricEvent>(new UnboundedChannelOptions { SingleReader = true });

    public ValueTask EnqueueAsync(ICommunityMetricEvent metricEvent, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(metricEvent, ct);

    public async IAsyncEnumerable<ICommunityMetricEvent> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var e in _channel.Reader.ReadAllAsync(ct))
            yield return e;
    }
}
```

---

## 5. Background Processor (HostedService)

**Ubicación**: `src/Services/CoppAddresd.Community/Metrics/CommunityMetricsProcessorHostedService.cs`

```csharp
using CoppAddresd.Community.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Community.Metrics;

public sealed class CommunityMetricsProcessorHostedService(
    ICommunityMetricsQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<CommunityMetricsProcessorHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Community metrics background processor started.");

        await foreach (var evt in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<CommunityDbContext>();

                switch (evt)
                {
                    case PostCreatedMetricEvent e:
                        await ProcessPostCreatedAsync(db, e, stoppingToken);
                        break;
                    case CommentCreatedMetricEvent e:
                        await ProcessCommentCreatedAsync(db, e, stoppingToken);
                        break;
                    case LikeAddedMetricEvent e:
                        await ProcessLikeAddedAsync(db, e, stoppingToken);
                        break;
                    case LikeRemovedMetricEvent e:
                        await ProcessLikeRemovedAsync(db, e, stoppingToken);
                        break;
                    case RepostCreatedMetricEvent e:
                        await ProcessRepostCreatedAsync(db, e, stoppingToken);
                        break;
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Error processing community metric event: {@Event}", evt);
            }
        }
    }

    // --- Upsert implementations ---

    private static async Task ProcessPostCreatedAsync(CommunityDbContext db, PostCreatedMetricEvent e, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO community.community_daily_metrics (metric_date, metric_key, dimension_key, total_count, last_updated_at)
            VALUES
                (@p0, 'posts_count', 'total', 1, NOW()),
                (@p0, 'posts_count', @p1, 1, NOW()),
                (@p0, 'hourly_activity', @p2, 1, NOW())
            ON CONFLICT (metric_date, metric_key, dimension_key)
            DO UPDATE SET
                total_count = community.community_daily_metrics.total_count + 1,
                last_updated_at = NOW();
            """;

        await db.Database.ExecuteSqlRawAsync(sql,
            [e.MetricDate, e.PostType, e.HourOfDay.ToString()], ct);
    }

    private static async Task ProcessCommentCreatedAsync(CommunityDbContext db, CommentCreatedMetricEvent e, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO community.community_daily_metrics (metric_date, metric_key, dimension_key, total_count, last_updated_at)
            VALUES
                (@p0, 'comments_count', 'total', 1, NOW()),
                (@p0, 'hourly_activity', @p1, 1, NOW())
            ON CONFLICT (metric_date, metric_key, dimension_key)
            DO UPDATE SET
                total_count = community.community_daily_metrics.total_count + 1,
                last_updated_at = NOW();
            """;

        await db.Database.ExecuteSqlRawAsync(sql,
            [e.MetricDate, e.HourOfDay.ToString()], ct);
    }

    private static async Task ProcessLikeAddedAsync(CommunityDbContext db, LikeAddedMetricEvent e, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO community.community_daily_metrics (metric_date, metric_key, dimension_key, total_count, last_updated_at)
            VALUES (@p0, 'likes_count', 'total', 1, NOW())
            ON CONFLICT (metric_date, metric_key, dimension_key)
            DO UPDATE SET
                total_count = community.community_daily_metrics.total_count + 1,
                last_updated_at = NOW();
            """;

        await db.Database.ExecuteSqlRawAsync(sql, [e.MetricDate], ct);
    }

    private static async Task ProcessLikeRemovedAsync(CommunityDbContext db, LikeRemovedMetricEvent e, CancellationToken ct)
    {
        const string sql = """
            UPDATE community.community_daily_metrics
            SET total_count = GREATEST(0, total_count - 1), last_updated_at = NOW()
            WHERE metric_date = @p0 AND metric_key = 'likes_count' AND dimension_key = 'total';
            """;

        await db.Database.ExecuteSqlRawAsync(sql, [e.MetricDate], ct);
    }

    private static async Task ProcessRepostCreatedAsync(CommunityDbContext db, RepostCreatedMetricEvent e, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO community.community_daily_metrics (metric_date, metric_key, dimension_key, total_count, last_updated_at)
            VALUES
                (@p0, 'reposts_count', 'total', 1, NOW()),
                (@p0, 'hourly_activity', @p1, 1, NOW())
            ON CONFLICT (metric_date, metric_key, dimension_key)
            DO UPDATE SET
                total_count = community.community_daily_metrics.total_count + 1,
                last_updated_at = NOW();
            """;

        await db.Database.ExecuteSqlRawAsync(sql,
            [e.MetricDate, e.HourOfDay.ToString()], ct);
    }
}
```

---

## 6. Puntos de inyección en CommunityMutation.cs

`CommunityMutation.cs` está en `src/Services/CoppAddresd.Community/GraphQL/Mutations/CommunityMutation.cs`. Los métodos HotChocolate inyectan dependencias via `[Service]`. **NO usan MediatR.**

### Patrón de inyección:
Agregar `[Service] ICommunityMetricsQueue metricsQueue` como parámetro en cada método relevante, y llamar `await metricsQueue.EnqueueAsync(new XxxMetricEvent(...), ct)` **DESPUÉS** del `await db.SaveChangesAsync(ct)`.

### 6.1. `CreatePost` (buscar el método por signatura)
```
// Buscar: public async Task<Post> CreatePost(...)
// Después del: await db.SaveChangesAsync(ct);
// Agregar parámetro: [Service] ICommunityMetricsQueue? metricsQueue = null
// Agregar enqueue:
if (metricsQueue != null)
    await metricsQueue.EnqueueAsync(new PostCreatedMetricEvent(
        post.Id,
        DateOnly.FromDateTime(now),
        post.Type?.ToString()?.ToLowerInvariant() ?? "general",
        now.Hour
    ), ct);
```

### 6.2. `CreatePollPost` (línea ~242)
```
// Misma lógica que CreatePost.
// PostType = "poll"
```

### 6.3. Método de comentarios (buscar: AddComment / CreateComment)
```
// Buscar método que crea un Comment y llama db.SaveChangesAsync
// Agregar parámetro: [Service] ICommunityMetricsQueue? metricsQueue = null
// Agregar enqueue:
if (metricsQueue != null)
    await metricsQueue.EnqueueAsync(new CommentCreatedMetricEvent(
        comment.Id,
        DateOnly.FromDateTime(comment.CreatedAt),
        comment.CreatedAt.Hour
    ), ct);
```

### 6.4. Método de likes/toggle like (buscar: LikePost / ToggleLike)
```
// Cuando se AGREGA un like:
await metricsQueue.EnqueueAsync(new LikeAddedMetricEvent(like.PostId, DateOnly.FromDateTime(now), now.Hour), ct);

// Cuando se ELIMINA un like:
await metricsQueue.EnqueueAsync(new LikeRemovedMetricEvent(like.PostId, DateOnly.FromDateTime(now)), ct);
```

### 6.5. Método de reposts (buscar: RepostPost / CreateRepost)
```
await metricsQueue.EnqueueAsync(new RepostCreatedMetricEvent(repost.Id, DateOnly.FromDateTime(now), now.Hour), ct);
```

---

## 7. Query handler de analítica (nuevo)

El dashboard ERP consume los datos via una query handler o endpoint HTTP. Crear en el proyecto Community:

**Ubicación**: `src/Services/CoppAddresd.Community/GraphQL/Queries/CommunityErpAnalyticsQuery.cs`

El query debe hacer:
```csharp
// 1. Rango de fechas (últimos 30 días por defecto)
var from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
var to = DateOnly.FromDateTime(DateTime.UtcNow);

// 2. Leer desde rollup (O(1)):
var metrics = await db.CommunityDailyMetrics.AsNoTracking()
    .Where(x => x.MetricDate >= from && x.MetricDate <= to)
    .ToListAsync(ct);

// 3. Transformar en DTOs: serie temporal de posts, comments, likes; distribución por tipo; horas pico.

// 4. Fallback si metrics está vacío: COUNT(*) sobre las tablas OLTP directamente.
```

---

## 8. DbContext — Agregar DbSet

En `CommunityDbContext.cs`, agregar:
```csharp
public DbSet<CommunityDailyMetric> CommunityDailyMetrics => Set<CommunityDailyMetric>();
```

Y asegurarse que `OnModelCreating` llame `ApplyConfigurationsFromAssembly` (ya lo hace).

---

## 9. DI Registration

En el `Program.cs` del proyecto `CoppAddresd.Community`, agregar **antes** del `app.Run()`:

```csharp
builder.Services.AddSingleton<ICommunityMetricsQueue, CommunityMetricsQueue>();
builder.Services.AddHostedService<CommunityMetricsProcessorHostedService>();
```

---

## 10. Migración EF Core

Crear la migración dentro del proyecto Community (schema `community.`):

```powershell
# Desde la raíz coppAddresdBack
dotnet ef migrations add AddCommunityDailyMetrics `
    --project src/Services/CoppAddresd.Community `
    --startup-project src/Services/CoppAddresd.Community `
    --context CommunityDbContext
```

La migración debe generar:
```sql
CREATE TABLE community.community_daily_metrics (
    metric_date date NOT NULL,
    metric_key character varying(64) NOT NULL,
    dimension_key character varying(64) NOT NULL,
    total_count bigint NOT NULL DEFAULT 0,
    last_updated_at timestamp without time zone NOT NULL,
    CONSTRAINT pk_community_daily_metrics PRIMARY KEY (metric_date, metric_key, dimension_key)
);
CREATE INDEX ix_community_daily_metrics_key_dim_date
    ON community.community_daily_metrics (metric_key, dimension_key, metric_date);
```

---

## 11. Tests unitarios a crear

Archivo: `tests/CoppAddresd.UnitTests/Community/CommunityMetricsProcessorTests.cs`

Scenarios a testear (usar InMemory EF o SQLite):
1. `PostCreated_Enqueues_IncrementsTotalAndType` — después de procesar `PostCreatedMetricEvent`, la tabla tiene 1 en `posts_count/total` y 1 en `posts_count/article`.
2. `PostCreated_TwiceSameDay_Accumulates` — dos eventos del mismo día suman correctamente.
3. `LikeAdded_ThenRemoved_IsZero` — add + remove = `total_count = 0` (no negativo).
4. `HourlyActivity_GetsUpdated` — `hourly_activity/14` se incrementa cuando `HourOfDay = 14`.
5. `CommentCreated_IncrementsSeparateKey` — no mezcla `comments_count` con `posts_count`.

---

## 12. Documentación a crear/actualizar

- Crear `coppAddresdBack/docs/modules/community/analytics.md` con el diseño de la tabla, los eventos y la estrategia de lectura.
- Actualizar `coppAddresdBack/docs/architecture/README.md` con ADR para Dashboard #5.
