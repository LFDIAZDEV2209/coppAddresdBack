using CoppAddresd.Auth.Configuration;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace CoppAddresd.Auth.Seeders;

/// <summary>
/// Crea cuentas de usuario para pacientes de DEMO que hoy no pueden iniciar
/// sesión en la app, de modo que las notificaciones por el canal
/// <c>community</c> dejen de quedar en <c>skipped</c> ("El paciente no tiene
/// cuenta en la app."). Cubre tres grupos:
///
/// <list type="number">
///   <item>Pacientes dueños de alertas cuyo documento sigue el patrón
///   <c>DEMO-{ESTADO}-{NNNN}</c> (los que aparecen en la bandeja de Alertas).</item>
///   <item>Pacientes históricos con documento <c>1000000001…</c> que aún no
///   tienen cuenta.</item>
///   <item>Perfiles de <c>community.profiles</c> cuyo <c>user_id</c> apunta a un
///   usuario inexistente en <c>auth.Users</c> (huérfanos): se crea el usuario
///   faltante respetando el Id ya referenciado, sin tocar el perfil.</item>
/// </list>
///
/// El Id de las cuentas nuevas es determinístico (MD5 del documento) para que el
/// seeder sea idempotente y no dependa del orden de arranque de los servicios.
/// No-op salvo que <c>PatientAccountDemo:Enabled</c> sea true.
/// </summary>
public static class PatientAccountDemoSeeder
{
    /// <summary>Prefijo del hash que genera los Ids determinísticos.</summary>
    private const string IdNamespace = "antares-patient-account:";

    private sealed record ExistingProfile(Guid Id, string? DocumentNumber, string? Email,
        string? FirstName, string? LastName);

    private sealed record OrphanRow(Guid UserId);

    /// <summary>Id determinístico de la cuenta de un paciente a partir del documento.</summary>
    public static Guid PatientUserId(string documentNumber)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes($"{IdNamespace}{documentNumber}"));
        return new Guid(hash);
    }

    public static async Task SeedAsync(
        AuthDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        PatientAccountDemoSettings settings,
        ILogger logger,
        CancellationToken ct = default)
    {
        if (settings is null || !settings.Enabled || string.IsNullOrWhiteSpace(settings.Password))
        {
            logger.LogInformation("PatientAccountDemo not configured, skipping");
            return;
        }

        var appApplication = await dbContext.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Code == "app", ct);

        if (appApplication is null)
        {
            logger.LogError("PatientAccountDemo aborted: application 'app' not found");
            return;
        }

        // 1) Pacientes sin cuenta: dueños de alertas (DEMO-*) y documentos 10000000xx.
        var appPassword = settings.Password;
        var pending = await LoadPatientsWithoutAccountAsync(dbContext, settings, ct);
        logger.LogInformation("PatientAccountDemo: {Count} pacientes sin cuenta", pending.Count);

        var created = 0;
        foreach (var profile in pending)
        {
            if (string.IsNullOrWhiteSpace(profile.DocumentNumber))
            {
                continue;
            }

            var email = !string.IsNullOrWhiteSpace(profile.Email)
                ? profile.Email!
                : $"demo+{profile.DocumentNumber.Replace("-", string.Empty).ToLowerInvariant()}@coppaddresd.com";

            var id = PatientUserId(profile.DocumentNumber);

            // Si el email ya pertenece a otro usuario, no duplicamos identidad.
            var existingByEmail = await userManager.FindByEmailAsync(email);
            if (existingByEmail is not null && existingByEmail.Id != id)
            {
                await LinkProfileAsync(dbContext, profile.Id, existingByEmail.Id, ct);
                continue;
            }

            var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    Id = id,
                    UserName = email,
                    Email = email,
                    FirstName = profile.FirstName ?? "Paciente",
                    LastName = profile.LastName ?? "Demo",
                    IsActive = true,
                    EmailConfirmed = true,
                };

                var result = await userManager.CreateAsync(user, appPassword);
                if (!result.Succeeded)
                {
                    logger.LogError("No se pudo crear la cuenta {Email}: {Errors}",
                        email, string.Join(", ", result.Errors.Select(e => e.Description)));
                    continue;
                }

                created++;
            }

            await EnsureAppAccessAsync(dbContext, user.Id, appApplication.Id, ct);
            await LinkProfileAsync(dbContext, profile.Id, user.Id, ct);
        }

        // 2) Perfiles de la comunidad huérfanos: existe el user_id referenciado
        //    pero no el usuario. Se crea el usuario con ESE Id (no se re-vincula
        //    el perfil, para no pisar el vínculo existente).
        var orphans = await LoadOrphanCommunityUserIdsAsync(dbContext, ct);
        logger.LogInformation("PatientAccountDemo: {Count} perfiles de comunidad huérfanos", orphans.Count);

        var orphanCreated = 0;
        foreach (var orphanId in orphans)
        {
            if (await dbContext.Users.AnyAsync(u => u.Id == orphanId, ct))
            {
                continue;
            }

            var shortId = orphanId.ToString("N")[..8];
            var email = $"demo+{shortId}@coppaddresd.com";

            var user = new ApplicationUser
            {
                Id = orphanId,
                UserName = email,
                Email = email,
                FirstName = "Miembro",
                LastName = "Demo",
                IsActive = true,
                EmailConfirmed = true,
            };

            var result = await userManager.CreateAsync(user, appPassword);
            if (!result.Succeeded)
            {
                logger.LogError("No se pudo crear la cuenta huérfana {Email}: {Errors}",
                    email, string.Join(", ", result.Errors.Select(e => e.Description)));
                continue;
            }

            await EnsureAppAccessAsync(dbContext, user.Id, appApplication.Id, ct);
            orphanCreated++;
        }

        logger.LogInformation(
            "PatientAccountDemo finalizado: {Created} cuentas de pacientes, {Orphans} cuentas de miembros de comunidad",
            created, orphanCreated);
    }

    /// <summary>
    /// Pacientes sin <c>user_id</c>: primero los dueños de alertas con documento
    /// <c>DEMO-*</c> (los de la bandeja) y luego los históricos <c>10000000xx</c>.
    /// </summary>
    private static async Task<IReadOnlyList<ExistingProfile>> LoadPatientsWithoutAccountAsync(
        AuthDbContext dbContext,
        PatientAccountDemoSettings settings,
        CancellationToken ct)
    {
        var limit = settings.MaxAccounts > 0 ? settings.MaxAccounts : 100000;

        return await dbContext.Database
            .SqlQueryRaw<ExistingProfile>(
                """
                SELECT p."id" AS "Id",
                       p."document_number" AS "DocumentNumber",
                       p."email" AS "Email",
                       p."first_name" AS "FirstName",
                       p."last_name" AS "LastName"
                FROM app.patient_profiles p
                WHERE p."user_id" IS NULL
                  AND p."document_number" IS NOT NULL
                  AND (
                        p."document_number" LIKE 'DEMO-%'
                        OR EXISTS (
                            SELECT 1 FROM app.health_test_alerts a
                            WHERE a."patient_id" = p."id"
                        )
                      )
                ORDER BY p."document_number"
                LIMIT {0}
                """,
                limit)
            .ToListAsync(ct);
    }

    /// <summary>
    /// <c>user_id</c> referenciados por <c>community.profiles</c> que no existen
    /// como usuario (huérfanos). Sin DbSet para el schema community, SQL crudo.
    /// </summary>
    private static async Task<IReadOnlyList<Guid>> LoadOrphanCommunityUserIdsAsync(
        AuthDbContext dbContext,
        CancellationToken ct)
    {
        var rows = await dbContext.Database
            .SqlQueryRaw<OrphanRow>(
                """
                SELECT DISTINCT p."user_id" AS "UserId"
                FROM community.profiles p
                WHERE p."user_id" IS NOT NULL
                  AND NOT EXISTS (
                        SELECT 1 FROM auth."Users" u WHERE u."Id" = p."user_id"
                      )
                """)
            .ToListAsync(ct);

        return rows.Select(r => r.UserId).ToList();
    }

    private static async Task EnsureAppAccessAsync(
        AuthDbContext dbContext, Guid userId, Guid appApplicationId, CancellationToken ct)
    {
        var already = await dbContext.UserApplications
            .AsNoTracking()
            .AnyAsync(ua => ua.UserId == userId && ua.ApplicationId == appApplicationId, ct);

        if (already)
        {
            return;
        }

        dbContext.UserApplications.Add(new UserApplication
        {
            UserId = userId,
            ApplicationId = appApplicationId,
            CreatedAt = DateTime.UtcNow,
        });
        await dbContext.SaveChangesAsync(ct);
    }

    private static async Task LinkProfileAsync(
        AuthDbContext dbContext, Guid profileId, Guid userId, CancellationToken ct) =>
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE app.patient_profiles SET \"user_id\" = {userId} WHERE \"id\" = {profileId}",
            ct);
}
