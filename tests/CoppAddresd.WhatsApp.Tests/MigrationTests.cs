using CoppAddresd.Domain.Entities.WhatsApp;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.WhatsApp.Tests;

[Trait("Category", "Integration")]
public sealed class MigrationTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Migration_IsIsolated_AndWamidIsUnique()
    {
        await using var db = fixture.CreateDb();
        Assert.Contains(await db.Database.GetAppliedMigrationsAsync(CancellationToken.None), x => x.EndsWith("InitialWhatsApp"));
        db.RawEvents.Add(new WhatsAppRawEvent { Wamid = "unique", Payload = "{}" });
        await db.SaveChangesAsync(CancellationToken.None);
        db.RawEvents.Add(new WhatsAppRawEvent { Wamid = "unique", Payload = "{}" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(CancellationToken.None));
        Assert.All(db.Model.GetEntityTypes(), x => Assert.Equal("whatsapp", x.GetSchema()));
    }
}
