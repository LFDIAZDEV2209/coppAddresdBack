using CoppAddresd.Auth.Constants;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Auth.Seeders;

/// <summary>
/// Siembra los permisos del módulo Program Progress (Program.*) en
/// <c>auth.permissions</c>. Idempotente por <c>code</c>, mismo patrón que
/// <see cref="PermissionSeeder"/>. Debe ejecutarse ANTES que
/// <see cref="AdminSeeder"/> para que el rol Admin reciba los 5 códigos por
/// convención (AdminSeeder asigna todos los permisos existentes al rol Admin).
///
/// Nota: los códigos viven aquí (no en <see cref="PermissionCodes"/>) para
/// mantener el cambio acotado al módulo. Los roles clínicos que consumen el
/// módulo se mapearán en <see cref="RoleSeeder"/> cuando lleguen las pantallas
/// ERP (batch B7).
/// </summary>
public static class ProgramProgressPermissionsSeeder
{
    /// <summary>Códigos del módulo Program Progress (SPEC §6.14).</summary>
    public static readonly string[] Codes =
    [
        "Program.View",
        "Program.Edit",
        "Program.Enroll",
        "Program.Adapt",
        "Program.ForceComplete",
        "Program.Export",
    ];

    public static async Task SeedAsync(AuthDbContext dbContext, ILogger logger, CancellationToken ct = default)
    {
        logger.LogInformation("Seeding program progress permissions...");

        var existingCodes = await dbContext.Permissions
            .Select(p => p.Code)
            .ToListAsync(ct);

        var permissionsToCreate = Codes
            .Where(code => !existingCodes.Contains(code))
            .Select(code => new Permission
            {
                Code = code,
                Name = FormatPermissionName(code),
                Module = PermissionCodes.GetModule(code),
                CreatedAt = DateTime.UtcNow
            })
            .ToList();

        if (permissionsToCreate.Count > 0)
        {
            dbContext.Permissions.AddRange(permissionsToCreate);
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("Created {Count} program progress permissions", permissionsToCreate.Count);
        }
        else
        {
            logger.LogInformation("All program progress permissions already exist");
        }
    }

    private static string FormatPermissionName(string code)
    {
        var parts = code.Split('.');
        if (parts.Length != 2) return code;

        var resource = parts[0];
        var action = parts[1];

        return $"{action} {resource.ToLower()}";
    }
}