using System.Text;
using CoppAddresd.Community;
using CoppAddresd.Community.GraphQL;
using CoppAddresd.Community.GraphQL.Mutations;
using CoppAddresd.Community.GraphQL.Queries;
using CoppAddresd.Community.GraphQL.Resolvers;
using CoppAddresd.Community.GraphQL.Subscriptions;
using CoppAddresd.Community.Messages;
using CoppAddresd.Community.Metrics;
using CoppAddresd.Community.Persistence;
using CoppAddresd.Community.Seeders;
using CoppAddresd.Community.Scheduling;
using CoppAddresd.Community.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
CoppAddresd.Shared.Security.ErpSessionValidationExtensions.AddErpSessionValidation(builder.Services, builder.Configuration);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection no configurada.");

builder.Services.AddDbContext<CommunityDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "community"));
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

builder.Services.AddHttpContextAccessor();

// Almacenamiento de objetos (imágenes de publicaciones). Proveedor según
// Storage:Provider (Local por defecto; S3/MinIO con Storage:S3).
builder.Services.AddCommunityStorage(builder.Configuration);

var jwt = builder.Configuration.GetSection("Jwt");
var secret = jwt["Secret"]!;
var issuer = jwt["Issuer"]!;
var audiences = jwt.GetSection("ValidAudiences").Get<string[]>() ?? ["app", "erp"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudiences = audiences,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Community.View", policy =>
        policy.RequireClaim("permission", "Community.View"));
    options.AddPolicy("CommunityModerator", policy =>
        policy.RequireClaim("permission", "Community.Moderate"));
    options.AddPolicy("Community.Manage", policy =>
        policy.RequireClaim("permission", "Community.Manage"));
});

var origins = builder.Configuration["Cors:Origins"]
    ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? ["http://localhost:3000", "http://localhost:5173"];
builder.Services.AddCors(options =>
{
    options.AddPolicy("CommunityCors", policy =>
        policy.WithOrigins(origins).AllowAnyMethod().AllowAnyHeader());
});

builder.Services
    .AddGraphQLServer()
    .AddQueryType<CommunityQuery>()
    .AddMutationType<CommunityMutation>()
.AddSubscriptionType<CommunitySubscription>()
    .AddTypeExtension<ClubQuery>()
    .AddTypeExtension<ClubMutation>()
    .AddTypeExtension<ClubSubscription>()
    .AddTypeExtension<CommunityErpAnalyticsQuery>()
    .AddType<PostImageUrlResolver>()
    .AddType<ProfileImageUrlResolver>()
    .AddTypeExtension<ProfileResolvers>()
    .AddTypeExtension<PollVoteResolvers>()
    .AddTypeExtension<ClubResolvers>()
    .AddTypeExtension<ClubEventResolvers>()
    .AddTypeExtension<ClubMediaResolvers>()
    .AddAuthorization()
    .AddInMemorySubscriptions()
    .AddSocketSessionInterceptor(_ => new SubscriptionAuthInterceptor(builder.Configuration));

// Publica posts de clubes PROGRAMADO vencidos (scheduler ligero del servicio).
builder.Services.AddHostedService<ClubPostScheduler>();

builder.Services.AddHealthChecks();

// Métricas del dashboard ERP (Dashboard #5): cola en memoria (Channel) + procesador
// en background que hace el UPSERT atómico sobre community.community_daily_metrics.
// El enqueue en las mutaciones no bloquea la request: el writer del Channel retorna
// prácticamente de inmediato y el HostedService drena la cola en segundo plano.
builder.Services.AddSingleton<ICommunityMetricsQueue, CommunityMetricsQueue>();

// Reconciliación del rollup al arrancar (idempotente, autoritativa): recupera los
// eventos perdidos por reinicios/cambios de instancia (la cola es en memoria).
// Se registra ANTES del procesador para reconstruir desde el OLTP antes de drenar
// eventos nuevos; si falla, se loguea y el arranque continúa.
builder.Services.AddScoped<ICommunityMetricsBackfillService, CommunityMetricsBackfillService>();
builder.Services.AddHostedService<CommunityMetricsBackfillHostedService>();

builder.Services.AddHostedService<CommunityMetricsProcessorHostedService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CommunityDbContext>();
    await db.Database.MigrateAsync();

    if (app.Environment.IsDevelopment())
    {
        await CommunitySeeder.SeedAsync(db, builder.Configuration);
        await ClubSeeder.SeedAsync(db, builder.Configuration, CancellationToken.None);
    }

    // Contenido demo adicional (polls/imágenes) — idempotente. No-op si
    // CommunityDemo no está configurado con Enabled=true (producción).
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var demo = builder.Configuration.GetSection(CommunityDemoSettings.SectionName).Get<CommunityDemoSettings>();
    if (demo is { Enabled: true })
    {
        await CommunityContentSeeder.SeedAsync(db, logger, CancellationToken.None);
    }
    else
    {
        logger.LogInformation("CommunityDemo not configured, skipping content seed");
    }
}

app.UseCors("CommunityCors");
app.UseAuthentication();
app.UseAuthorization();
app.UseWebSockets();

app.MapGraphQL("/api/v1/community/graphql").WithOptions(o => o.Tool.Enable = false);
app.MapHealthChecks("/health");
app.MapGraphQLWebSocket("/api/v1/community/subscriptions");
app.MapPostStorageEndpoints();

// Endpoint interno ERP → Community (X-Internal-Key) para entregar mensajes
// directos del perfil de sistema (notificaciones de alertas de tests de salud).
app.MapInternalMessageEndpoints();

// Mantenimiento interno (X-Internal-Key): reconciliación del rollup de métricas.
app.MapMaintenanceEndpoints();

app.Run();
