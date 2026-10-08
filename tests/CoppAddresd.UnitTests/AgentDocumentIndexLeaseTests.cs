using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Infrastructure.Persistence;
using CoppAddresd.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.UnitTests;

/// <summary>Prueba SQL real del compare-and-set usado para recuperar la indexación.</summary>
public sealed class AgentDocumentIndexLeaseTests
{
    private sealed class DocumentsDb(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            // Solo este agregado: las otras tablas tienen tipos exclusivos de PostgreSQL.
            foreach (var property in typeof(AppDbContext).GetProperties()
                .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>)))
                builder.Ignore(property.PropertyType.GenericTypeArguments[0]);
            var document = builder.Entity<AgentDocument>();
            document.Ignore(x => x.KnowledgeBase);
            document.Property(x => x.CreatedAt).HasConversion(
                v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero));
            document.Property(x => x.UpdatedAt).HasConversion(
                v => v.HasValue ? (long?)v.Value.UtcTicks : null,
                v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : null);
        }
    }

    [Fact]
    public async Task Reserva_ExcluyeSegundoIntento_YRecuperaProcesandoVencido()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new DocumentsDb(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var now = DateTimeOffset.UtcNow;
        var document = new AgentDocument
        {
            Id = Guid.NewGuid(), KnowledgeBaseId = Guid.NewGuid(), StorageKey = "guide.md", FileName = "guide.md",
            Status = AgentDocumentStatus.Procesando, CreatedAt = now.AddDays(-1),
        };
        db.AgentDocuments.Add(document);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var repository = new AgentCatalogRepository(db);
        Assert.True(await repository.TryStartDocumentIndexAsync(document.Id, now, now.AddMinutes(-15)));
        Assert.False(await repository.TryStartDocumentIndexAsync(document.Id, now.AddSeconds(1), now.AddMinutes(-15)));
        var later = now.AddMinutes(16);
        Assert.True(await repository.TryStartDocumentIndexAsync(document.Id, later, later.AddMinutes(-15)));
        // Un resultado de la reserva anterior no puede sobrescribir al reintento.
        document.Status = AgentDocumentStatus.Error;
        document.ErrorMessage = "Resultado anterior";
        Assert.False(await repository.CompleteDocumentIndexAsync(document, now));
        document.Status = AgentDocumentStatus.Listo;
        document.ErrorMessage = null;
        document.ChunksCount = 4;
        document.UpdatedAt = later.AddSeconds(2);
        Assert.True(await repository.CompleteDocumentIndexAsync(document, later));
        var stored = await repository.GetDocumentAsync(document.Id);
        Assert.Equal(AgentDocumentStatus.Listo, stored!.Status);
        Assert.Equal(4, stored.ChunksCount);
    }

    [Fact]
    public async Task DocumentoEliminado_NoSeRecreaAlFinalizarIndexacion()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new DocumentsDb(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var repository = new AgentCatalogRepository(db);
        var document = new AgentDocument { Id = Guid.NewGuid(), Status = AgentDocumentStatus.Listo };
        Assert.False(await repository.CompleteDocumentIndexAsync(document, DateTimeOffset.UtcNow));
        Assert.Empty(await db.AgentDocuments.ToListAsync());
    }
}
