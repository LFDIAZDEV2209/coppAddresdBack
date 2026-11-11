using CoppAddresd.Application.Features.LegalDocuments;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Migrations;
using CoppAddresd.Infrastructure.Persistence;
using CoppAddresd.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Options;
using Npgsql;
using NSubstitute;

namespace CoppAddresd.IntegrationTests;

/// <summary>Only a unique disposable loopback database; a few synthetic legal rows, no external services.</summary>
public sealed class LegalConsentPersistenceTests : IAsyncLifetime
{
    private string database = "";
    private string connection = "";
    private string server = "";
    private readonly Guid user = Guid.NewGuid();
    private readonly LegalConsentSettings config = new() { EnforcementEnabled = true };
    private readonly IOptionsMonitor<LegalConsentSettings> settings = Substitute.For<IOptionsMonitor<LegalConsentSettings>>();

    public async Task InitializeAsync()
    {
        settings.CurrentValue.Returns(config);
        server = Environment.GetEnvironmentVariable("COP_TEST_DB_CONNECTION") ?? "";
        if (server.Length == 0) return;
        var source = new NpgsqlConnectionStringBuilder(server);
        if (source.Host is not ("127.0.0.1" or "localhost" or "::1"))
            throw new InvalidOperationException("Consent tests require a loopback PostgreSQL server.");
        database = $"copp_consent_test_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(server) { Database = "postgres" }.ConnectionString);
        await admin.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE DATABASE \"{database}\" TEMPLATE template0", admin))
            await command.ExecuteNonQueryAsync();
        connection = new NpgsqlConnectionStringBuilder(server) { Database = database }.ConnectionString;
        await using var db = Context();
        await db.Database.ExecuteSqlRawAsync("CREATE SCHEMA app; CREATE SCHEMA erp;");
        // Apply exact production migration operations; the audit-role GRANT is not needed by this
        // disposable owner-only fixture and would otherwise require a cluster-wide app_user role.
        await Apply(db, new InitialAuditSchema(), up: true, skipGrants: true);
        await Apply(db, new AddLegalDocuments(), up: true);
        await Apply(db, new AddLegalDocumentsAudit(), up: true);
        await Apply(db, new AddLegalConsentReceipts(), up: true);
    }

    public async Task DisposeAsync()
    {
        if (database.Length == 0) return;
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(server) { Database = "postgres" }.ConnectionString);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", admin);
        await command.ExecuteNonQueryAsync();
    }

    private AppDbContext Context()
    {
        var actor = Substitute.For<IAuditActorContext>();
        actor.ActorType.Returns(AuditActorType.User);
        actor.UserId.Returns(user);
        actor.RequestId.Returns("synthetic-consent-request");
        return new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection, p => p.EnableRetryOnFailure())
            .AddInterceptors(new AuditTriggerInterceptor(actor)).Options);
    }

    private LegalConsentService Service(AppDbContext db) => new(db, settings, TimeProvider.System);

    private static async Task Apply(AppDbContext db, Migration migration, bool up, bool skipGrants = false)
    {
        var operations = (up ? migration.UpOperations : migration.DownOperations)
            .Where(op => !skipGrants || op is not SqlOperation sql || !sql.Sql.TrimStart().StartsWith("GRANT", StringComparison.Ordinal)).ToArray();
        foreach (var command in db.GetService<IMigrationsSqlGenerator>().Generate(operations, db.Model))
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
    }

    [NutritionPostgresFact]
    public async Task Concurrent_acceptance_is_one_receipt_with_actor_audit_and_withdrawal_blocks_reuse()
    {
        await using var db = Context();
        var doc = new LegalDocument { Id = Guid.NewGuid(), Code = "synthetic-disclosure-es", Title = "Synthetic" };
        var version = new LegalDocumentVersion { Id = Guid.NewGuid(), DocumentId = doc.Id, Major = 1,
            Content = "Synthetic fixture only", IsPublished = true, CreatedAt = DateTime.UtcNow };
        doc.CurrentVersionId = version.Id;
        db.LegalDocuments.Add(doc); db.LegalDocumentVersions.Add(version);
        await db.SaveChangesAsync();
        config.Scopes["chat"] = new() { Approved = true, ProcessingRevision = "synthetic-v1",
            Providers = ["synthetic-provider"], Documents = new() { ["es"] = doc.Code } };
        var status = (await Service(db).GetStatusAsync(user, "es", default)).Scopes.Single(s => s.Scope == "chat");
        var request = new AcceptLegalConsentRequest("chat", version.Id, "es", status.DisclosureId!, Guid.NewGuid());
        async Task<LegalConsentReceiptDto> Accept()
        {
            await using var context = Context();
            return await Service(context).AcceptAsync(user, request, default);
        }
        var receipts = await Task.WhenAll(Accept(), Accept());
        Assert.Equal(receipts[0].Id, receipts[1].Id);
        Assert.Equal(1, await db.LegalDocumentAcceptances.CountAsync());
        var audit = await db.ActivityLogs.AsNoTracking().SingleAsync(x => x.TableName == "legal_document_acceptances");
        Assert.Equal(user, audit.UserId);
        Assert.Equal("synthetic-consent-request", audit.RequestId);
        config.Scopes["chat"].Providers = ["synthetic-provider-revision"];
        var superseded = (await Service(db).GetStatusAsync(user, "es", default)).Scopes.Single(s => s.Scope == "chat");
        Assert.False(superseded.Accepted);
        Assert.Equal(receipts[0].Id, superseded.ReceiptId);
        Assert.Null((await Service(db).GetStatusAsync(Guid.NewGuid(), "es", default)).Scopes.Single(s => s.Scope == "chat").ReceiptId);
        await Service(db).WithdrawAsync(user, superseded.ReceiptId!.Value, default);
        Assert.Null((await Service(db).GetStatusAsync(user, "es", default)).Scopes.Single(s => s.Scope == "chat").ReceiptId);
        Assert.Equal(403, (await Assert.ThrowsAsync<AiConsentException>(() => Service(db).EnsureUserAsync(user, "chat", default))).StatusCode);
        Assert.NotNull((await Service(db).AcceptAsync(user, request, default)).WithdrawnAt);
        Assert.Equal(2, await db.ActivityLogs.CountAsync(x => x.TableName == "legal_document_acceptances"));
    }

    [NutritionPostgresFact]
    public async Task Migration_roundtrip_preserves_existing_legal_documents_and_published_content_cannot_change()
    {
        await using var db = Context();
        var document = Guid.NewGuid(); var version = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO erp.legal_documents (id,code,title) VALUES ({document},'synthetic-es','Synthetic')");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO erp.legal_document_versions (id,document_id,major,minor,is_published,content) VALUES ({version},{document},1,0,true,'Synthetic immutable text')");
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE erp.legal_document_versions SET content='Changed' WHERE id={version}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        await Apply(db, new AddLegalConsentReceipts(), up: false);
        Assert.Equal(1, await db.LegalDocuments.CountAsync());
        Assert.Equal("Synthetic immutable text", (await db.LegalDocumentVersions.SingleAsync()).Content);
        await Apply(db, new AddLegalConsentReceipts(), up: true);
        Assert.Equal(0, await db.LegalDocumentAcceptances.CountAsync());
    }
}
