using CoppAddresd.Auth.Constants;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Auth.Seeders;

public static class PermissionSeeder
{
    public static async Task SeedAsync(AuthDbContext dbContext, ILogger logger, CancellationToken ct = default)
    {
        logger.LogInformation("Seeding permissions...");

        var existingPermissions = await dbContext.Permissions
            .Select(p => p.Code)
            .ToListAsync(ct);

        var permissionsToCreate = new List<Permission>();

        foreach (var code in PermissionCodes.GetAll())
        {
            if (!existingPermissions.Contains(code))
            {
                permissionsToCreate.Add(new Permission
                {
                    Code = code,
                    Name = FormatPermissionName(code),
                    Module = PermissionCodes.GetModule(code),
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        if (permissionsToCreate.Count > 0)
        {
            dbContext.Permissions.AddRange(permissionsToCreate);
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("Created {Count} permissions", permissionsToCreate.Count);
        }
        else
        {
            logger.LogInformation("All permissions already exist");
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
