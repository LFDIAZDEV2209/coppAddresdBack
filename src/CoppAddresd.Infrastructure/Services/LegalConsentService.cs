using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CoppAddresd.Application.Features.LegalDocuments;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Infrastructure.Services;

public sealed class LegalConsentService(AppDbContext db, IOptionsMonitor<LegalConsentSettings> settings,
    TimeProvider clock) : ILegalConsentService
{
    private static string Locale(string locale) => locale is "es" or "en" ? locale
        : throw new AiConsentException("CONSENT_LOCALE_UNSUPPORTED", "", 400);
    private static AiConsentException Unavailable(string scope) => new("CONSENT_DOCUMENT_UNAVAILABLE", scope, 503);
    private static LegalConsentReceiptDto Receipt(LegalDocumentAcceptance row) => new(row.Id, row.Scope,
        row.LegalDocumentVersionId, row.Locale, row.DisclosureId, row.AcceptedAt, row.WithdrawnAt);

    private LegalConsentScopeSettings Config(string scope)
    {
        if (!LegalConsentScopes.All.Contains(scope, StringComparer.Ordinal)
            || !settings.CurrentValue.Scopes.TryGetValue(scope, out var config) || !config.Approved
            || string.IsNullOrWhiteSpace(config.ProcessingRevision)
            || config.Providers.Any(p => string.IsNullOrWhiteSpace(p) || p.Length > 120)
            || (scope != LegalConsentScopes.Program && scope != LegalConsentScopes.Sos && config.Providers.Length == 0))
            throw Unavailable(scope);
        return config;
    }

    private static string DisclosureSnapshot(string scope, LegalConsentScopeSettings config) => JsonSerializer.Serialize(new
        {
            scope, revision = config.ProcessingRevision,
            providers = config.Providers.Order(StringComparer.Ordinal).Distinct(StringComparer.Ordinal).ToArray(),
        });

    private async Task<(LegalConsentDocumentDto Document, string DisclosureId, string[] Providers, string Snapshot)> CurrentAsync(
        string scope, string locale, CancellationToken ct)
    {
        var config = Config(scope);
        if (!config.Documents.TryGetValue(locale, out var code) || string.IsNullOrWhiteSpace(code)) throw Unavailable(scope);
        var document = await db.LegalDocuments.AsNoTracking().Include(x => x.Versions)
            .SingleOrDefaultAsync(x => x.Code == code, ct);
        var version = document?.Versions.SingleOrDefault(x => x.Id == document.CurrentVersionId && x.IsPublished);
        if (document is null || version is null || string.IsNullOrWhiteSpace(version.Content)) throw Unavailable(scope);
        var snapshot = DisclosureSnapshot(scope, config);
        var disclosureId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot))).ToLowerInvariant();
        return (new(document.Code, version.Id, version.VersionLabel, document.Title, version.Content,
            locale, version.CreatedAt), disclosureId, config.Providers.ToArray(), snapshot);
    }

    public async Task<LegalConsentStatusDto> GetStatusAsync(Guid userId, string locale, CancellationToken ct)
    {
        locale = Locale(locale);
        // La retirada no depende de que el documento o procesador siga publicado.
        // Un único lote limitado a una fila por ámbito evita cargar todo el historial.
        var withdrawable = (await db.LegalDocumentAcceptances.AsNoTracking()
            .Where(x => x.UserId == userId && x.WithdrawnAt == null && LegalConsentScopes.All.Contains(x.Scope))
            .GroupBy(x => x.Scope)
            .Select(group => group.OrderByDescending(x => x.AcceptedAt).ThenByDescending(x => x.Id).First())
            .ToListAsync(ct)).ToDictionary(x => x.Scope, StringComparer.Ordinal);
        var result = new List<LegalConsentScopeDto>();
        foreach (var scope in LegalConsentScopes.All)
        {
            withdrawable.TryGetValue(scope, out var latestActive);
            try
            {
                var current = await CurrentAsync(scope, locale, ct);
                var accepted = await db.LegalDocumentAcceptances.AsNoTracking().Where(x => x.UserId == userId
                    && x.Scope == scope && x.Locale == locale && x.LegalDocumentVersionId == current.Document.VersionId
                    && x.DisclosureId == current.DisclosureId && x.WithdrawnAt == null)
                    .OrderByDescending(x => x.AcceptedAt).FirstOrDefaultAsync(ct);
                var receipt = accepted ?? latestActive;
                result.Add(new(scope, true, accepted is not null, current.DisclosureId, current.Providers,
                    [current.Document], receipt?.Id, receipt?.AcceptedAt));
            }
            catch (AiConsentException ex) when (ex.StatusCode == 503)
            { result.Add(new(scope, false, false, null, [], [], latestActive?.Id, latestActive?.AcceptedAt)); }
        }
        return new(locale, result);
    }

    public Task<LegalConsentReceiptDto> AcceptAsync(Guid userId, AcceptLegalConsentRequest request, CancellationToken ct)
    {
        if (userId == Guid.Empty || request.VersionId == Guid.Empty || request.IdempotencyKey == Guid.Empty
            || request.DisclosureId?.Length != 64)
            throw new AiConsentException("CONSENT_REQUEST_INVALID", request.Scope ?? "", 400);
        Locale(request.Locale);
        return MutateAsync(userId, async () =>
        {
            var prior = await db.LegalDocumentAcceptances.AsNoTracking()
                .SingleOrDefaultAsync(x => x.UserId == userId && x.IdempotencyKey == request.IdempotencyKey, ct);
            if (prior is not null)
            {
                if (prior.Scope != request.Scope || prior.LegalDocumentVersionId != request.VersionId
                    || prior.Locale != request.Locale || prior.DisclosureId != request.DisclosureId)
                    throw new AiConsentException("CONSENT_REQUEST_CONFLICT", request.Scope, 409);
                return Receipt(prior);
            }
            // Serialize publication against acceptance. Publish updates this same document row.
            if (db.Database.IsNpgsql())
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM erp.legal_documents WHERE id = (SELECT document_id FROM erp.legal_document_versions WHERE id = {request.VersionId}) FOR SHARE", ct);
            var current = await CurrentAsync(request.Scope, request.Locale, ct);
            if (current.Document.VersionId != request.VersionId || current.DisclosureId != request.DisclosureId)
                throw new AiConsentException("DOCUMENT_VERSION_CHANGED", request.Scope, 409);
            var row = new LegalDocumentAcceptance { Id = Guid.NewGuid(), UserId = userId,
                LegalDocumentVersionId = request.VersionId, Scope = request.Scope, Locale = request.Locale,
                DisclosureId = request.DisclosureId, IdempotencyKey = request.IdempotencyKey,
                DisclosureSnapshot = current.Snapshot,
                AcceptedAt = clock.GetUtcNow().UtcDateTime };
            db.LegalDocumentAcceptances.Add(row);
            await db.SaveChangesAsync(ct);
            return Receipt(row);
        }, ct);
    }

    public async Task WithdrawAsync(Guid userId, Guid receiptId, CancellationToken ct)
    {
        await MutateAsync(userId, async () =>
        {
            var row = await db.LegalDocumentAcceptances.SingleOrDefaultAsync(x => x.Id == receiptId && x.UserId == userId, ct)
                ?? throw new NotFoundException("Consent receipt not found.");
            if (row.WithdrawnAt is null)
            {
                // Withdraw the scope, including retries/other active receipts in other locales.
                var active = await db.LegalDocumentAcceptances.Where(x => x.UserId == userId
                    && x.Scope == row.Scope && x.WithdrawnAt == null).ToListAsync(ct);
                foreach (var receipt in active) receipt.WithdrawnAt = clock.GetUtcNow().UtcDateTime;
                await db.SaveChangesAsync(ct);
            }
            return true;
        }, ct);
    }

    public async Task EnsureUserAsync(Guid userId, string scope, CancellationToken ct)
    {
        var config = Config(scope);
        var available = false;
        foreach (var locale in config.Documents.Keys.Where(x => x is "es" or "en"))
        {
            try
            {
                var current = await CurrentAsync(scope, locale, ct);
                available = true;
                if (userId != Guid.Empty && await db.LegalDocumentAcceptances.AsNoTracking().AnyAsync(x => x.UserId == userId
                    && x.Scope == scope && x.Locale == locale && x.LegalDocumentVersionId == current.Document.VersionId
                    && x.DisclosureId == current.DisclosureId && x.WithdrawnAt == null, ct)) return;
            }
            catch (AiConsentException ex) when (ex.StatusCode == 503) { }
        }
        if (!available) throw Unavailable(scope);
        throw new AiConsentException("CONSENT_REQUIRED", scope, 403);
    }

    public async Task EnsurePatientAsync(Guid patientId, string scope, CancellationToken ct)
    {
        var userId = await db.PatientProfiles.AsNoTracking().Where(x => x.Id == patientId)
            .Select(x => x.UserId).SingleOrDefaultAsync(ct);
        await EnsureUserAsync(userId ?? Guid.Empty, scope, ct);
    }

    private Task<T> MutateAsync<T>(Guid userId, Func<Task<T>> action, CancellationToken ct)
        => db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            if (!db.Database.IsRelational()) return await action();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            if (db.Database.IsNpgsql())
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({userId.ToString()}, 0))", ct);
            var result = await action();
            await transaction.CommitAsync(ct);
            return result;
        });
}
