using CoppAddresd.Auth.Constants;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Auth.Seeders;

/// <summary>
/// Siembra el permiso granular del módulo SOS real (change sos-panic-real):
/// <c>Sos.Alerts.Manage</c> permite al staff del ERP atender alertas de
/// pánico de pacientes. Idempotente por <c>code</c>, mismo patrón que
/// <see cref="ProgramProgressPermissionsSeeder"/> y
/// <see cref="HealthTestsPermissionsSeeder"/>. Debe ejecutarse ANTES que
/// <see cref="AdminSeeder"/> para que el rol Admin reciba el código por
/// convención (AdminSeeder asigna todos los permisos existentes al Admin).
/// </summary>
public static class SosPermissionsSeeder
{
    /// <summary>Códigos del módulo SOS.</summary>
    public static readonly string[] Codes = ["Sos.Alerts.Manage"];

    public static async Task SeedAsync(
        AuthDbContext dbContext,
        ILogger logger,
        CancellationToken ct = default
    )
    {
        logger.LogInformation("Seeding SOS permissions...");

        var existingCodes = await dbContext.Permissions.Select(p => p.Code).ToListAsync(ct);

        var permissionsToCreate = Codes
            .Where(code => !existingCodes.Contains(code))
            .Select(code => new Permission
            {
                Code = code,
                Name = FormatPermissionName(code),
                Module = PermissionCodes.GetModule(code),
                CreatedAt = DateTime.UtcNow,
            })
            .ToList();

        if (permissionsToCreate.Count > 0)
        {
            dbContext.Permissions.AddRange(permissionsToCreate);
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("Created {Count} SOS permissions", permissionsToCreate.Count);
        }
        else
        {
            logger.LogInformation("All SOS permissions already exist");
        }
    }

    private static string FormatPermissionName(string code)
    {
        var parts = code.Split('.');
        if (parts.Length != 2)
        {
            return code;
        }

        return $"{parts[1]} {parts[0].ToLower()}";
    }
}
