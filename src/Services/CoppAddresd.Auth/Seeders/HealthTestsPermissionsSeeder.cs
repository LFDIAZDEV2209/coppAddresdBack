using CoppAddresd.Auth.Constants;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Auth.Seeders;

/// <summary>
/// Siembra los permisos del módulo Tests de Salud (HealthTests.*) en
/// <c>auth.permissions</c>. Idempotente por <c>code</c>, mismo patrón que
/// <see cref="ProgramProgressPermissionsSeeder"/>. Debe ejecutarse ANTES que
/// <see cref="AdminSeeder"/> para que el rol Admin reciba los 5 códigos por
/// convención (AdminSeeder asigna todos los permisos existentes al rol Admin).
///
/// Nota: los códigos viven aquí (no en <see cref="PermissionCodes"/>) para
/// mantener el cambio acotado al módulo. Los roles clínicos se mapean en
/// <see cref="RoleSeeder"/> (el rol Professional recibirá ViewOwn/View por
/// convención; Admin recibe todo).
/// </summary>
public static class HealthTestsPermissionsSeeder
{
    /// <summary>Códigos del módulo Tests de Salud (SPEC pacientes/health-tests).</summary>
    public static readonly string[] Codes =
    [
        "HealthTests.View",
        "HealthTests.ViewOwn",
        "HealthTests.Manage",
        "HealthTests.Assign",
        "HealthTests.Review",
    ];

    public static async Task SeedAsync(
        AuthDbContext dbContext,
        ILogger logger,
        CancellationToken ct = default
    )
    {
        logger.LogInformation("Seeding health tests permissions...");

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
                "Created {Count} health tests permissions",
                permissionsToCreate.Count
            );
        }
        else
        {
            logger.LogInformation("All health tests permissions already exist");
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
