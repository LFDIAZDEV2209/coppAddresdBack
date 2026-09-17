using CoppAddresd.Auth.Avatar.Application;
using CoppAddresd.Auth.Data;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Auth.Infrastructure.Avatar;

public sealed class AvatarPreferenceStore(AuthDbContext db) : IAvatarPreferenceStore
{
    public Task<string?> ReadAsync(Guid userId, CancellationToken ct) => db.UserPreferences.AsNoTracking()
        .Where(p => p.UserId == userId).Select(p => p.AvatarConfiguration).SingleOrDefaultAsync(ct);

    // Mismo acceso de solo lectura al perfil que utiliza PatientLookupService.
    public Task<string?> ProfileGenderAsync(Guid userId, CancellationToken ct) => db.Database
        .SqlQuery<string?>($"""SELECT gender AS "Value" FROM app.patient_profiles WHERE user_id = {userId}""")
        .FirstOrDefaultAsync(ct);

    // Upsert atómico: conserva idioma/acento incluso en escrituras concurrentes.
    public async Task WriteAsync(Guid userId, string configuration, CancellationToken ct) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO auth."UserPreferences" ("UserId", "AvatarConfiguration", "UpdatedAt")
            VALUES ({userId}, CAST({configuration} AS jsonb), {DateTime.UtcNow})
            ON CONFLICT ("UserId") DO UPDATE SET
                "AvatarConfiguration" = EXCLUDED."AvatarConfiguration", "UpdatedAt" = EXCLUDED."UpdatedAt"
            """, ct);
}
