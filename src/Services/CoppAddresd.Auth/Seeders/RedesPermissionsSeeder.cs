using CoppAddresd.Auth.Constants;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Auth.Seeders;

/// <summary>
/// Siembra los permisos del módulo Acceso a Redes (Redes.*) en
/// <c>auth.permissions</c>: radicaciones de autorizaciones y facturación
/// RIPS de las redes prestadoras. Idempotente por <c>code</c>, mismo patrón
/// que <see cref="HealthTestsPermissionsSeeder"/>. Debe ejecutarse ANTES que
/// <see cref="AdminSeeder"/> para que el rol Admin los reciba por convención.
/// </summary>
public static class RedesPermissionsSeeder
{
    public static readonly string[] Codes =
    [
        "Redes.View",
        "Redes.Manage",
    ];

    public static async Task SeedAsync(
        AuthDbContext dbContext,
        ILogger logger,
        CancellationToken ct = default
    )
    {
        logger.LogInformation("Seeding redes permissions...");

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
            logger.LogInformation(
                "Created {Count} redes permissions",
                permissionsToCreate.Count
            );
        }
        else
        {
            logger.LogInformation("All redes permissions already exist");
        }
    }

    private static string FormatPermissionName(string code)
    {
        var parts = code.Split('.');
        if (parts.Length != 2)
            return code;

        var resource = parts[0];
        var action = parts[1];

        return $"{action} {resource.ToLower()}";
    }
}
