using CoppAddresd.Auth.Configuration;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace CoppAddresd.Auth.Seeders;

/// <summary>
/// Siembra los usuarios demo de la comunidad ANTARES (desarrollo): crea cada
/// usuario en <c>auth.Users</c> con un Id determinístico (derivado del número de
/// documento), le otorga acceso a la aplicación "app" y lo vincula a una fila de
/// <c>app.patient_profiles</c> por número de documento. Así la app móvil puede
/// iniciar sesión con <c>documentNumber + password + application:"app"</c>.
///
/// El Id determinístico es el contrato con el seeder de contenido del Community
/// service (<c>CommunityContentSeeder</c>): ambos derivan el mismo Guid a partir
/// del documento, por lo que no importa el orden de arranque de los servicios.
///
/// Es idempotente (no duplica usuarios si se ejecuta varias veces) y es un
/// no-op si la sección <c>CommunityDemo</c> no está configurada con Enabled=true.
/// </summary>
public static class CommunityDemoSeeder
{
    /// <summary>Usuarios demo: documento, correo y nombres. La contraseña es
    /// común para todos y se configura en <c>CommunityDemo:Password</c>.</summary>
    private sealed record DemoUser(string DocumentNumber, string Email, string FirstName, string LastName);

    /// <summary>Mantener sincronizado con la lista del Community service
    /// (<c>CommunityContentSeeder</c>): mismo documento → mismo Id de usuario.</summary>
    private static readonly IReadOnlyList<DemoUser> Users =
    [
        new("1000000001", "valentina.rios@demo.antares.co", "Valentina", "Ríos"),
        new("1000000002", "andres.cardenas@demo.antares.co", "Andrés", "Cárdenas"),
        new("1000000003", "carolina.mendoza@demo.antares.co", "Carolina", "Mendoza"),
        new("1000000004", "jorge.herrera@demo.antares.co", "Jorge", "Herrera"),
        new("1000000005", "luisa.fernandez@demo.antares.co", "Luisa", "Fernández"),
        new("1000000006", "miguel.pena@demo.antares.co", "Miguel Ángel", "Peña"),
        new("1000000007", "diana.ospina@demo.antares.co", "Diana", "Ospina"),
        new("1000000008", "camilo.restrepo@demo.antares.co", "Camilo", "Restrepo"),
        new("1000000009", "paola.salazar@demo.antares.co", "Paola", "Salazar"),
        new("1000000010", "santiago.pineda@demo.antares.co", "Santiago", "Pineda"),
        new("1000000011", "maria.castillo@demo.antares.co", "María", "Castillo"),
        new("1000000012", "david.quiroga@demo.antares.co", "David", "Quiroga"),
        new("1000000013", "laura.serna@demo.antares.co", "Laura", "Serna"),
        new("1000000014", "felipe.montoya@demo.antares.co", "Felipe", "Montoya"),
    ];

    /// <summary>
    /// Id determinístico de un usuario demo a partir de su documento. Debe
    /// coincidir exactamente con la función homónima del Community service.
    /// </summary>
    public static Guid DemoUserId(string documentNumber)
    {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes($"antares-community-demo:{documentNumber}"));
        return new Guid(hash);
    }

    public static async Task SeedAsync(
        AuthDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        CommunityDemoSettings settings,
        ILogger logger,
        CancellationToken ct = default)
    {
        if (settings is null || !settings.Enabled || string.IsNullOrWhiteSpace(settings.Password))
        {
            logger.LogInformation("CommunityDemo not configured, skipping");
            return;
        }

        logger.LogInformation("Seeding community demo users ({Count})...", Users.Count);

        // La aplicación "app" debe existir (la crea ApplicationSeeder, que corre
        // antes). Sin ella no podemos otorgar acceso.
        var appApplication = await dbContext.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Code == "app", ct);

        var seeded = 0;
        foreach (var u in Users)
        {
            var userId = DemoUserId(u.DocumentNumber);
            var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);

            if (user is null)
            {
                user = new ApplicationUser
                {
                    Id = userId,
                    UserName = u.Email,
                    Email = u.Email,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    IsActive = true,
                    EmailConfirmed = true,
                };

                var userResult = await userManager.CreateAsync(user, settings.Password);
                if (!userResult.Succeeded)
                {
                    logger.LogError("Failed to create community demo user {Email}: {Errors}",
                        u.Email, string.Join(", ", userResult.Errors.Select(e => e.Description)));
                    continue;
                }

                logger.LogInformation("Community demo user created: {Email} (doc {DocumentNumber})",
                    u.Email, u.DocumentNumber);
            }

            if (appApplication is not null)
            {
                var alreadyAssigned = await dbContext.UserApplications
                    .AsNoTracking()
                    .AnyAsync(ua => ua.UserId == user.Id && ua.ApplicationId == appApplication.Id, ct);

                if (!alreadyAssigned)
                {
                    dbContext.UserApplications.Add(new UserApplication
                    {
                        UserId = user.Id,
                        ApplicationId = appApplication.Id,
                        CreatedAt = DateTime.UtcNow,
                    });
                    await dbContext.SaveChangesAsync(ct);
                    logger.LogInformation("App access assigned to demo user {Email}", u.Email);
                }
            }

            await EnsurePatientProfileAsync(dbContext, user, u, logger, ct);
            seeded++;
        }

        logger.LogInformation("Community demo users seeded: {Count}", seeded);
    }

    /// <summary>
    /// Vincula el usuario a una fila de <c>app.patient_profiles</c> por número de
    /// documento (SQL crudo, sin cambiar el modelo del AuthDbContext). Si la fila
    /// no existe, la crea con el documento del usuario demo.
    /// </summary>
    private static async Task EnsurePatientProfileAsync(
        AuthDbContext dbContext, ApplicationUser user, DemoUser demo, ILogger logger, CancellationToken ct)
    {
        var existing = await dbContext.Database
            .SqlQueryRaw<PatientProfileIdRow>(
                "SELECT \"id\" AS \"Id\" FROM app.patient_profiles WHERE \"document_number\" = {0} LIMIT 1",
                demo.DocumentNumber)
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE app.patient_profiles SET \"user_id\" = {user.Id}, \"email\" = COALESCE(\"email\", {demo.Email}) WHERE \"id\" = {existing.Id}",
                ct);
            return;
        }

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO app.patient_profiles (\"id\", \"user_id\", \"first_name\", \"last_name\", \"document_number\", \"email\", \"status\", \"created_at\") VALUES ({Guid.NewGuid()}, {user.Id}, {demo.FirstName}, {demo.LastName}, {demo.DocumentNumber}, {demo.Email}, 'Activo', {DateTime.UtcNow})",
            ct);
        logger.LogInformation("Patient profile created for demo user {Email}", demo.Email);
    }

    /// <summary>Fila de proyección para la consulta raw que resuelve el id de un
    /// perfil de paciente por número de documento (mapeo por alias "Id").</summary>
    private sealed record PatientProfileIdRow
    {
        public Guid Id { get; set; }
    }
}