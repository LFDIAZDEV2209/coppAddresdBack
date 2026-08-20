using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CoppAddresd.Telemedicine.Infrastructure.Services;

/// <summary>
/// Resuelve la configuración operativa efectiva con caché corta: fila de la
/// clínica si existe; si no, la global de la organización; si no, defaults.
/// La configuración cambia poco y se lee en cada operación de agendamiento.
/// </summary>
public sealed class TelemedicineSettingsProvider(
    TelemedicineDbContext dbContext,
    IMemoryCache cache) : ITelemedicineSettingsProvider
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan NegativeCacheTtl = TimeSpan.FromMinutes(1);

    public async Task<TelemedicineSettings> GetSettingsAsync(
        Guid organizationId,
        Guid? clinicId,
        CancellationToken ct = default)
    {
        var key = $"tele-settings:{organizationId}:{clinicId?.ToString() ?? "global"}";

        return (await cache.GetOrCreateAsync(key, async entry =>
        {
            ct.ThrowIfCancellationRequested();
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;

            // Preferencia: clínica → organización → defaults.
            var clinicRow = clinicId is { } cid
                ? await dbContext.Settings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.OrganizationId == organizationId && s.ClinicId == cid, ct)
                : null;

            var orgRow = await dbContext.Settings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.OrganizationId == organizationId && s.ClinicId == null, ct);

            var effective = clinicRow ?? orgRow;
            if (effective is null)
            {
                entry.AbsoluteExpirationRelativeToNow = NegativeCacheTtl;
                return new TelemedicineSettings { OrganizationId = organizationId, ClinicId = clinicId };
            }

            if (clinicRow is not null)
            {
                // Merge: la fila de clínica es completa; sin lógica de merge por columna.
                effective.OrganizationId = organizationId;
            }

            return effective;
        }))!;
    }
}
