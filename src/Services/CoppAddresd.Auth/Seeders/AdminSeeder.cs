using CoppAddresd.Auth.Configuration;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Auth.Seeders;

public static class AdminSeeder
{
    public static async Task SeedAsync(
        AuthDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        AuthSettings authSettings,
        ILogger logger,
        CancellationToken ct = default
    )
    {
        logger.LogInformation("Seeding admin role and user...");

        const string adminRoleName = "Admin";

        var adminRole = await roleManager.FindByNameAsync(adminRoleName);
        if (adminRole is null)
        {
            adminRole = new ApplicationRole
            {
                Name = adminRoleName,
                Description = "Administrador del sistema con acceso total",
                IsActive = true,
                IsSystem = true,
            };

            var roleResult = await roleManager.CreateAsync(adminRole);
            if (!roleResult.Succeeded)
            {
                logger.LogError(
                    "Failed to create admin role: {Errors}",
                    string.Join(", ", roleResult.Errors.Select(e => e.Description))
                );
                return;
            }

            logger.LogInformation("Admin role created");
        }

        var adminUser = await userManager.FindByEmailAsync(authSettings.AdminEmail);
        if (adminUser is null)
        {
            adminUser = new ApplicationUser
            {
                UserName = authSettings.AdminEmail,
                Email = authSettings.AdminEmail,
                FirstName = authSettings.AdminFirstName,
                LastName = authSettings.AdminLastName,
                IsActive = true,
                EmailConfirmed = true,
            };

            var userResult = await userManager.CreateAsync(adminUser, authSettings.AdminPassword);

            if (!userResult.Succeeded)
            {
                logger.LogError(
                    "Failed to create admin user: {Errors}",
                    string.Join(", ", userResult.Errors.Select(e => e.Description))
                );

                return;
            }

            logger.LogInformation("Admin user created: {Email}", authSettings.AdminEmail);
        }
        else
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(adminUser);

            var passwordResult = await userManager.ResetPasswordAsync(
                adminUser,
                token,
                authSettings.AdminPassword
            );

            if (!passwordResult.Succeeded)
            {
                logger.LogError(
                    "Failed to reset admin password: {Errors}",
                    string.Join(", ", passwordResult.Errors.Select(e => e.Description))
                );

                return;
            }

            logger.LogInformation(
                "Admin password reset successfully for {Email}",
                authSettings.AdminEmail
            );
        }

        var isInRole = await userManager.IsInRoleAsync(adminUser, adminRoleName);
        if (!isInRole)
        {
            var addToRoleResult = await userManager.AddToRoleAsync(adminUser, adminRoleName);
            if (!addToRoleResult.Succeeded)
            {
                logger.LogError(
                    "Failed to assign admin role: {Errors}",
                    string.Join(", ", addToRoleResult.Errors.Select(e => e.Description))
                );
                return;
            }

            logger.LogInformation("Admin role assigned to {Email}", authSettings.AdminEmail);
        }

        var allPermissions = await dbContext.Permissions.ToListAsync(ct);
        var adminRolePermissions = await dbContext
            .RolePermissions.Where(rp => rp.RoleId == adminRole.Id)
            .Select(rp => rp.PermissionId)
            .ToListAsync(ct);

        var permissionsToAssign = allPermissions
            .Where(p => !adminRolePermissions.Contains(p.Id))
            .Select(p => new RolePermission { RoleId = adminRole.Id, PermissionId = p.Id })
            .ToList();

        if (permissionsToAssign.Count > 0)
        {
            dbContext.RolePermissions.AddRange(permissionsToAssign);
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation(
                "Assigned {Count} permissions to admin role",
                permissionsToAssign.Count
            );
        }
    }
}
