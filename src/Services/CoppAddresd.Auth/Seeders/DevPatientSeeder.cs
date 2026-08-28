using CoppAddresd.Auth.Configuration;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Auth.Seeders;

/// <summary>
/// Siembra un usuario paciente de desarrollo con acceso a la aplicación "app" y
/// lo vincula a una fila de <c>app.patient_profiles</c> por número de documento,
/// de modo que la app móvil pueda iniciar sesión con
/// <c>documentNumber + password + application:"app"</c>.
/// Es un no-op en producción: si la sección <c>DevPatient</c> no está configurada,
/// no crea nada.
/// </summary>
public static class DevPatientSeeder
{
    public static async Task SeedAsync(
        AuthDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        DevPatientSettings settings,
        ILogger logger,
        CancellationToken ct = default)
    {
        if (settings is null ||
            string.IsNullOrWhiteSpace(settings.DocumentNumber) ||
            string.IsNullOrWhiteSpace(settings.Email))
        {
            logger.LogInformation("DevPatient not configured, skipping");
            return;
        }

        logger.LogInformation("Seeding dev patient...");

        var devUser = await userManager.FindByEmailAsync(settings.Email);
        if (devUser is null)
        {
            devUser = new ApplicationUser
            {
                UserName = settings.Email,
                Email = settings.Email,
                FirstName = settings.FirstName,
                LastName = settings.LastName,
                IsActive = true,
                EmailConfirmed = true
            };

            var userResult = await userManager.CreateAsync(devUser, settings.Password);
            if (!userResult.Succeeded)
            {
                logger.LogError("Failed to create dev patient user: {Errors}",
                    string.Join(", ", userResult.Errors.Select(e => e.Description)));
                return;
            }

            logger.LogInformation("Dev patient user created: {Email}", settings.Email);
        }

        // La aplicación "app" debe existir (la crea ApplicationSeeder, que corre
        // antes). Sin ella no podemos otorgar acceso, así que abortamos.
        var appApplication = await dbContext.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Code == "app", ct);

        if (appApplication is null)
        {
            logger.LogError("Dev patient seeding aborted: application 'app' not found");
            return;
        }

        var alreadyAssigned = await dbContext.UserApplications
            .AsNoTracking()
            .AnyAsync(ua => ua.UserId == devUser.Id && ua.ApplicationId == appApplication.Id, ct);

        if (!alreadyAssigned)
        {
            dbContext.UserApplications.Add(new UserApplication
            {
                UserId = devUser.Id,
                ApplicationId = appApplication.Id,
                CreatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("App access assigned to dev patient {Email}", settings.Email);
        }

        // Vincular el paciente: sin DbSet para app.patient_profiles, usamos SQL
        // crudo (sin migración ni cambios al modelo de AuthDbContext). Proyectamos
        // a un registro con columna nombrada "Id" (el query raw escalar de EF
        // compone SELECT s."Value" y falla con columna real).
        // Buscamos por user_id o document_number (sin excluir soft-deleted) para
        // evitar violar el índice único ix_patient_profiles_user_id si ya existe
        // un perfil asignado a este usuario.
        var patientRow = await dbContext.Database
            .SqlQueryRaw<PatientIdRow>(
                """
                SELECT "id" AS "Id"
                FROM app.patient_profiles
                WHERE "user_id" = {0} OR "document_number" = {1}
                ORDER BY (CASE WHEN "user_id" = {0} THEN 0 ELSE 1 END)
                LIMIT 1
                """,
                devUser.Id,
                settings.DocumentNumber)
            .FirstOrDefaultAsync(ct);

        var patientId = patientRow?.Id ?? Guid.Empty;

        if (patientId != Guid.Empty)
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE app.patient_profiles
                SET "user_id" = {devUser.Id},
                    "document_number" = COALESCE("document_number", {settings.DocumentNumber}),
                    "email" = COALESCE("email", {settings.Email}),
                    "first_name" = COALESCE("first_name", {settings.FirstName}),
                    "last_name" = COALESCE("last_name", {settings.LastName}),
                    "status" = 'Activo',
                    "deleted_at" = NULL
                WHERE "id" = {patientId}
                """,
                ct);
            logger.LogInformation("Linked/updated existing patient profile {PatientId} for dev user {Email}",
                patientId, settings.Email);
        }
        else
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO app.patient_profiles (\"id\", \"user_id\", \"first_name\", \"last_name\", \"document_number\", \"email\", \"status\", \"created_at\") VALUES ({Guid.NewGuid()}, {devUser.Id}, {settings.FirstName}, {settings.LastName}, {settings.DocumentNumber}, {settings.Email}, 'Activo', {DateTime.UtcNow})",
                ct);
            logger.LogInformation("Created patient profile for dev user {Email}", settings.Email);
        }
    }

    /// <summary>
    /// Fila de proyección para la consulta raw que resuelve el <c>id</c> de un
    /// perfil de paciente por número de documento. El mapeo se hace por nombre
    /// con el alias <c>AS "Id"</c>.
    /// </summary>
    private sealed record PatientIdRow
    {
        public Guid Id { get; set; }
    }
}
