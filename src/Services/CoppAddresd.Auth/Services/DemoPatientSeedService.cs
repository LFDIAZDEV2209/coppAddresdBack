using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Auth.Services;

/// <summary>
/// Resultado del seed de un paciente demo: credenciales para pruebas locales.
/// </summary>
public sealed record DemoPatientSeedResult(
    Guid UserId,
    Guid PatientId,
    string? Email,
    string Password,
    bool Created
);

/// <summary>
/// Aprovisiona (o reutiliza) la cuenta de un paciente demo para pruebas locales:
/// crea el usuario en <c>auth.users</c> con password conocida, garantiza el
/// acceso a la aplicación <c>app</c> (<c>auth.user_applications</c>) y vincula
/// el perfil del paciente (<c>app.patient_profiles.user_id</c>) si está libre.
/// Idempotente: la 2ª corrida reutiliza el usuario existente sin tocar su password.
/// </summary>
public interface IDemoPatientSeedService
{
    Task<DemoPatientSeedResult> SeedPatientAsync(
        Guid patientId,
        string firstName,
        string lastName,
        string email,
        string password,
        CancellationToken ct = default
    );

    /// <summary>
    /// Fija (o reemplaza) la password de un usuario demo existente (dev).
    /// Se usa para dar credenciales conocidas a los profesionales del seed
    /// (creados sin password) sin tocar su rol, scopes ni aplicaciones.
    /// </summary>
    Task<bool> ResetDemoPasswordAsync(string email, string password);
}

public sealed class DemoPatientSeedService(
    UserManager<ApplicationUser> userManager,
    AuthDbContext dbContext
) : IDemoPatientSeedService
{
    private const string ApplicationCode = "app";

    public async Task<DemoPatientSeedResult> SeedPatientAsync(
        Guid patientId,
        string firstName,
        string lastName,
        string email,
        string password,
        CancellationToken ct = default
    )
    {
        // ¿El perfil del paciente ya está vinculado a un usuario?
        var linkedUserId = await dbContext
            .Database.SqlQueryRaw<Guid?>(
                """SELECT user_id AS "Value" FROM app.patient_profiles WHERE id = {0}""",
                patientId
            )
            .FirstOrDefaultAsync(ct);

        if (linkedUserId is { } existingUserId)
        {
            var existing = await userManager.FindByIdAsync(existingUserId.ToString());
            if (existing is not null)
            {
                await EnsureApplicationAccessAsync(existing.Id, ct);
                return new DemoPatientSeedResult(
                    existing.Id,
                    patientId,
                    existing.Email,
                    password,
                    Created: false
                );
            }
        }

        // ¿Existe ya un usuario con ese correo? Se vincula sin duplicar.
        var byEmail = await userManager.FindByEmailAsync(email);
        if (byEmail is not null)
        {
            await LinkPatientAsync(patientId, byEmail.Id, ct);
            await EnsureApplicationAccessAsync(byEmail.Id, ct);
            return new DemoPatientSeedResult(
                byEmail.Id,
                patientId,
                byEmail.Email,
                password,
                Created: false
            );
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            IsActive = true,
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"No se pudo crear el usuario demo: {string.Join(", ", result.Errors.Select(e => e.Description))}"
            );
        }

        await LinkPatientAsync(patientId, user.Id, ct);
        await EnsureApplicationAccessAsync(user.Id, ct);

        return new DemoPatientSeedResult(user.Id, patientId, email, password, Created: true);
    }

    public async Task<bool> ResetDemoPasswordAsync(string email, string password)
    {
        // FindByEmailAsync exige NormalizedEmail poblado; los usuarios del seed
        // de profesionales se insertaron sin él, así que se resuelve por Email.
        var user = await userManager.Users.FirstOrDefaultAsync(
            u => u.Email!.ToLower() == email.ToLower(),
            CancellationToken.None
        );
        if (user is null)
        {
            return false;
        }

        // Rellena el NormalizedEmail ausente: sin él el login por email falla.
        if (user.NormalizedEmail is null)
        {
            user.NormalizedEmail = userManager.NormalizeEmail(email);
            await userManager.UpdateAsync(user);
        }

        var remove = await userManager.RemovePasswordAsync(user);
        if (!remove.Succeeded)
        {
            return false;
        }

        var add = await userManager.AddPasswordAsync(user, password);
        return add.Succeeded;
    }

    private async Task LinkPatientAsync(Guid patientId, Guid userId, CancellationToken ct)
    {
        const string sql = """
            UPDATE app.patient_profiles
            SET user_id = {0}
            WHERE id = {1}
              AND user_id IS NULL
            """;
        await dbContext.Database.ExecuteSqlRawAsync(sql, new object[] { userId, patientId }, ct);
    }

    private async Task EnsureApplicationAccessAsync(Guid userId, CancellationToken ct)
    {
        var application = await dbContext
            .Applications.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Code == ApplicationCode && a.IsActive, ct);

        if (application is null)
        {
            throw new InvalidOperationException(
                $"La aplicación '{ApplicationCode}' no existe o está inactiva."
            );
        }

        var hasAccess = await dbContext
            .UserApplications.AsNoTracking()
            .AnyAsync(ua => ua.UserId == userId && ua.ApplicationId == application.Id, ct);

        if (!hasAccess)
        {
            dbContext.UserApplications.Add(
                new UserApplication
                {
                    UserId = userId,
                    ApplicationId = application.Id,
                    CreatedAt = DateTime.UtcNow,
                }
            );
            await dbContext.SaveChangesAsync(ct);
        }
    }
}
