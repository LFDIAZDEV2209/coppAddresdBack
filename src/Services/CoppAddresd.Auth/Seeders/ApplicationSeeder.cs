using CoppAddresd.Auth.Constants;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Auth.Seeders;

/// <summary>
/// Siembra las aplicaciones conocidas (APP, ERP), otorga acceso al usuario
/// admin al ERP y retro-asigna los refresh tokens emitidos antes de que
/// existiera el binding por aplicación (se ligan al ERP, única aplicación
/// cliente existente hasta entonces).
/// </summary>
public static class ApplicationSeeder
{
    public static async Task SeedAsync(
        AuthDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        string adminEmail,
        ILogger logger,
        CancellationToken ct = default)
    {
        logger.LogInformation("Seeding applications...");

        var erp = await EnsureApplicationAsync(dbContext, new Application
        {
            Code = ApplicationCodes.Erp,
            Name = "ERP",
            Description = "Aplicación administrativa para gestionar la plataforma",
            IsActive = true
        }, logger, ct);

        await EnsureApplicationAsync(dbContext, new Application
        {
            Code = ApplicationCodes.App,
            Name = "App Móvil",
            Description = "Aplicación móvil para pacientes y usuarios finales",
            IsActive = true
        }, logger, ct);

        // El usuario admin de prueba debe poder ingresar al ERP (única
        // aplicación que consume el sistema actualmente).
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser is not null)
        {
            var alreadyAssigned = await dbContext.UserApplications
                .AsNoTracking()
                .AnyAsync(ua => ua.UserId == adminUser.Id && ua.ApplicationId == erp.Id, ct);

            if (!alreadyAssigned)
            {
                dbContext.UserApplications.Add(new UserApplication
                {
                    UserId = adminUser.Id,
                    ApplicationId = erp.Id,
                    CreatedAt = DateTime.UtcNow
                });
                await dbContext.SaveChangesAsync(ct);
                logger.LogInformation("ERP access assigned to admin user {Email}", adminEmail);
            }
        }

        // Retro-asignación de tokens legacy: refresh tokens sin aplicación
        // ligada se asocian al ERP.
        var backfilled = await dbContext.RefreshTokens
            .Where(rt => rt.ApplicationId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.ApplicationId, erp.Id), ct);

        if (backfilled > 0)
        {
            logger.LogInformation("Backfilled {Count} legacy refresh tokens to ERP application", backfilled);
        }
    }

    private static async Task<Application> EnsureApplicationAsync(
        AuthDbContext dbContext,
        Application application,
        ILogger logger,
        CancellationToken ct)
    {
        var existing = await dbContext.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Code == application.Code, ct);

        if (existing is not null)
        {
            return existing;
        }

        application.Id = Guid.NewGuid();
        dbContext.Applications.Add(application);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Application {Code} created", application.Code);

        return application;
    }
}