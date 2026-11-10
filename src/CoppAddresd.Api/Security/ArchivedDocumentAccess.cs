using System.Security.Claims;
using CoppAddresd.Api.Context;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Api.Security;

/// <summary>Frontera de lectura del archivo de laboratorio nuevo. No cambia las
/// claves planas de documentos ERP ni los demás espacios del storage.</summary>
public sealed class ArchivedDocumentAccess(
    IDocumentRepository documents, IPatientRepository patients, ICurrentContext context)
{
    public static bool IsArchiveKey(string key)
        => CoppAddresd.Application.Common.Storage.DocumentArchiveStorageKeys.IsProtected(key);

    public async Task<Document?> ResolveReadableAsync(string key, ClaimsPrincipal actor,
        bool hasReadCapability, CancellationToken ct)
    {
        var parts = key.Split('/');
        if (parts.Length != 4 || parts[0] != "documents"
            || !Guid.TryParseExact(parts[1], "D", out var patientId)
            || !Guid.TryParseExact(parts[2], "D", out var documentId)
            || string.IsNullOrWhiteSpace(parts[3])) return null;
        var document = await documents.GetByIdAsync(documentId, ct);
        if (document is null || document.DeletedAt is not null || document.PatientId != patientId
            || document.Patient?.DeletedAt is not null || document.Status is not ("Ready" or "Archived")) return null;
        var prefix = $"documents/{patientId:D}/{documentId:D}/";
        if (!document.StorageKey.StartsWith(prefix + "original.", StringComparison.Ordinal)
            || document.StorageKey[prefix.Length..].Contains('/')
            || (key != document.StorageKey && key != prefix + "extraction.json")) return null;

        // Como el presign GET de S3, una firma válida es una capacidad de lectura
        // temporal emitida solo después de autorizar al dueño o al equipo.
        if (hasReadCapability) return document;
        if (context.UserId is not { } userId) return null;
        if (actor.HasClaim("aud", "app"))
        {
            var patient = await patients.GetByUserIdAsync(userId, ct);
            return patient is { DeletedAt: null } && patient.Id == patientId ? document : null;
        }
        if (!actor.HasClaim("aud", "erp") || !await context.HasPermissionAsync("Documents.View", ct)) return null;
        if (context.ActiveClinicId is { } clinicId && document.ClinicId != clinicId) return null;
        if (context.ActiveOrganizationId is { } organizationId && document.Clinic?.OrganizationId != organizationId) return null;
        return document;
    }
}
