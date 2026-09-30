using CoppAddresd.Auth.Constants;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Auth.Seeders;

/// <summary>
/// Siembra los permisos del módulo de medios multimedia (Media.*) en
/// <c>auth.permissions</c> (change erp-program-content-admin): View, Create,
/// Edit, Publish, Archive y Delete para la biblioteca de podcasts del programa
/// de 83 días. Idempotente por <c>code</c>, mismo patrón que
/// <see cref="ProgramProgressPermissionsSeeder"/>,
/// <see cref="HealthTestsPermissionsSeeder"/> y <see cref="SosPermissionsSeeder"/>.
/// Debe ejecutarse ANTES que <see cref="AdminSeeder"/> para que el rol Admin
/// reciba el conjunto completo Media.* por convención (AdminSeeder asigna todos
/// los permisos existentes al rol Admin). Los roles clínicos se mapean en
/// <see cref="RoleSeeder"/> (ClinicalDirector: gestión editorial;
/// Professional: solo Media.View).
/// </summary>
public static class MediaPermissionsSeeder
{
    /// <summary>Códigos del módulo Media (change erp-program-content-admin).</summary>
    public static readonly string[] Codes =
    [
        PermissionCodes.MediaView,
        PermissionCodes.MediaCreate,
        PermissionCodes.MediaEdit,
        PermissionCodes.MediaPublish,
        PermissionCodes.MediaArchive,
        PermissionCodes.MediaDelete,
    ];

    public static async Task SeedAsync(
        AuthDbContext dbContext,
        ILogger logger,
        CancellationToken ct = default
    )
    {
        logger.LogInformation("Seeding media permissions...");

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
            logger.LogInformation("Created {Count} media permissions", permissionsToCreate.Count);
        }
        else
        {
            logger.LogInformation("All media permissions already exist");
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
