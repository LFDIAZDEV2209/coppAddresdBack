using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CoppAddresd.Api.Constants;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Infrastructure.Persistence;
using CoppAddresd.Infrastructure.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using System.IdentityModel.Tokens.Jwt;

namespace CoppAddresd.UnitTests.ProgramProgress.Api;

// ============================================================================
// Infraestructura compartida de los tests de la API del módulo (T-14/T-15):
// BD PostgreSQL aislada (mismo patrón que ProgramRepositoryTestDb) + host de
// prueba (WebApplicationFactory) + emisión de JWTs de test + helpers de seed.
// Requiere COP_TEST_DB_CONNECTION; si no está definida, los tests se omiten.
// ============================================================================

/// <summary>
/// Definición de la colección de tests de la API del módulo. Ambos archivos
/// (AuthorizationTests y ContractTests) comparten UNA BD aislada por corrida
/// (<see cref="ProgramApiTestDb"/>) y corren SECUENCIALMENTE: las variables de
/// entorno que configuran el host de prueba se fijan una sola vez en
/// <see cref="ProgramApiTestDb.InitializeAsync"/> (el host las lee de los
/// sources por defecto de <c>WebApplication.CreateBuilder</c>, que sí ve
/// variables de entorno en las lecturas eager del <c>Program.cs</c>).
/// </summary>
[CollectionDefinition(Name)]
public sealed class ProgramApiTestCollection : ICollectionFixture<ProgramApiTestDb>
{
    public const string Name = "program-progress-api";
}

/// <summary>
/// BD aislada para los tests de la API de Progreso del Programa: crea
/// <c>coppaddresd_prog_api_test_&lt;guid&gt;</c>, el esquema <c>auth</c> mínimo
/// que referencian las FKs y aplica la cadena completa de migraciones
/// (incluye <c>AddAuditActionWidening</c> y
/// <c>AddProgramProgressAuditCoverage</c>). Al inicializar fija las variables
/// de entorno que el host de prueba consume.
/// </summary>
public sealed class ProgramApiTestDb : IAsyncLifetime
{
    private const string EnvVar = "COP_TEST_DB_CONNECTION";

    private readonly string? _baseConnectionString = Environment.GetEnvironmentVariable(EnvVar);
    private readonly string _testDbName =
        $"coppaddresd_prog_api_test_{Guid.NewGuid():N}"[..57];

    public bool Skipped { get; private set; }

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(_baseConnectionString))
        {
            Skipped = true;
            return;
        }

        var builder = new NpgsqlConnectionStringBuilder(_baseConnectionString)
        {
            Database = _testDbName,
        };
        ConnectionString = builder.ConnectionString;

        await ExecuteOnBaseAsync(async (cmd, ct) =>
        {
            cmd.CommandText = """
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'app_user') THEN
                        CREATE ROLE app_user;
                    END IF;
                END
                $$;
                """;
            await cmd.ExecuteNonQueryAsync(ct);
        });

        var locale = await DetectAvailableLocaleAsync();

        await ExecuteOnBaseAsync(async (cmd, ct) =>
        {
            cmd.CommandText = $"""
                DROP DATABASE IF EXISTS "{_testDbName}" WITH (FORCE);
                CREATE DATABASE "{_testDbName}"
                    TEMPLATE template0
                    ENCODING 'UTF8'
                    LC_COLLATE '{locale}'
                    LC_CTYPE '{locale}';
                """;
            await cmd.ExecuteNonQueryAsync(ct);
        });

        // Esquema auth mínimo: las migraciones crean FKs por SQL hacia
        // auth."Users"("Id") (tabla gestionada por el Auth Service en prod).
        await ExecuteOnTestAsync(async (cmd, ct) =>
        {
            cmd.CommandText = """
                CREATE SCHEMA IF NOT EXISTS auth;
                CREATE TABLE IF NOT EXISTS auth."Users" ("Id" uuid NOT NULL PRIMARY KEY);
                """;
            await cmd.ExecuteNonQueryAsync(ct);
        });

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();

        ApplyHostEnvironmentVariables();
    }

    /// <summary>
    /// Configura el host de prueba vía variables de entorno: son un source por
    /// defecto de <c>WebApplication.CreateBuilder</c> y sí se ven en las
    /// lecturas eager del <c>Program.cs</c> (a diferencia de
    /// <c>ConfigureAppConfiguration</c>, que la WebApplicationFactory aplica
    /// después de las lecturas top-level). Mismos valores para toda la
    /// colección (corren en secuencia, sin carrera).
    /// </summary>
    private void ApplyHostEnvironmentVariables()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", ConnectionString);
        Environment.SetEnvironmentVariable("Jwt__Secret", TestJwt.Secret);
        Environment.SetEnvironmentVariable("Jwt__Issuer", TestJwt.Issuer);
        Environment.SetEnvironmentVariable("Storage__SignatureKey", "test-storage-signature-key-for-api-tests");
        Environment.SetEnvironmentVariable("Storage__Provider", "Local");
        Environment.SetEnvironmentVariable("Program__DefaultTemplate__Code", "default-83w");
        Environment.SetEnvironmentVariable("Program__Streak__FreezeGrantEveryPerfectDays", "7");
        Environment.SetEnvironmentVariable("Fcm__Enabled", "false");
        Environment.SetEnvironmentVariable("Fcm__ServiceAccountJson", "");
        // Sin servicios externos: el seeder de agentes falla rápido (conexión
        // rechazada) y el try/catch del seeder lo captura.
        Environment.SetEnvironmentVariable("AiService__BaseUrl", "http://127.0.0.1:1");
        Environment.SetEnvironmentVariable("AuthService__BaseUrl", "http://127.0.0.1:1");
        Environment.SetEnvironmentVariable("AuthService__InternalApiKey", "test-internal-key");
    }

    public async Task DisposeAsync()
    {
        if (Skipped || _baseConnectionString is null)
        {
            return;
        }

        try
        {
            NpgsqlConnection.ClearAllPools();
            await ExecuteOnBaseAsync(async (cmd, ct) =>
            {
                cmd.CommandText = $"DROP DATABASE IF EXISTS \"{_testDbName}\" WITH (FORCE);";
                await cmd.ExecuteNonQueryAsync(ct);
            });
        }
        catch
        {
            // No op: la limpieza no debe tumbar la suite.
        }
    }

    public AppDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options);

    private Task ExecuteOnBaseAsync(Func<NpgsqlCommand, CancellationToken, Task> action)
        => ExecuteAsync(_baseConnectionString!, action);

    private Task ExecuteOnTestAsync(Func<NpgsqlCommand, CancellationToken, Task> action)
        => ExecuteAsync(ConnectionString, action);

    private async Task<string> DetectAvailableLocaleAsync()
    {
        string? detected = null;
        await ExecuteOnBaseAsync(async (cmd, ct) =>
        {
            cmd.CommandText = """
                SELECT CASE
                    WHEN EXISTS (SELECT 1 FROM pg_collation
                                 WHERE collname IN ('en_US.utf8', 'en_US.UTF-8'))
                    THEN 'en_US.utf8'
                    ELSE (SELECT datcollate FROM pg_database WHERE datname = 'postgres' LIMIT 1)
                END;
                """;
            detected = await cmd.ExecuteScalarAsync(ct) as string;
        });

        return string.IsNullOrWhiteSpace(detected) ? "C" : detected;
    }

    private static async Task ExecuteAsync(
        string connectionString, Func<NpgsqlCommand, CancellationToken, Task> action)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        await action(command, CancellationToken.None);
    }
}

/// <summary>
/// Host de la API bajo prueba: WebApplicationFactory con configuración
/// in-memory (conexión a la BD de test, JWT, storage Local sin AWS). El
/// cliente expone helper para inyectar el Bearer de un JWT de test.
///
/// Se usa <see cref="CoppAddresd.Api.Controllers.ProgramController"/> como
/// marcador de assembly: el tipo <c>Program</c> es ambiguo en los tests
/// (CoppAddresd.Api y CoppAddresd.Auth generan ambos un entry point) y
/// WebApplicationFactory solo necesita un tipo del assembly de la API para
/// localizar su entry point.
/// </summary>
public sealed class ProgramApiHost : IAsyncDisposable
{
    private readonly WebApplicationFactory<CoppAddresd.Api.Controllers.ProgramController> _factory;

    private ProgramApiHost(WebApplicationFactory<CoppAddresd.Api.Controllers.ProgramController> factory)
        => _factory = factory;

    public static ProgramApiHost Create(ProgramApiTestDb fixture)
    {
        // La configuración llega por variables de entorno (fijadas por el
        // fixture): la WebApplicationFactory aplica ConfigureAppConfiguration
        // DESPUÉS de las lecturas eager del Program.cs, que exigen
        // Storage:SignatureKey y ConnectionStrings:DefaultConnection en
        // tiempo de arranque. El entorno "Testing" evita cargar
        // appsettings.Development.json (provider S3, FCM habilitado).
        var factory = new WebApplicationFactory<CoppAddresd.Api.Controllers.ProgramController>()
            .WithWebHostBuilder(builder => builder
                .UseEnvironment("Testing"));

        return new ProgramApiHost(factory);
    }

    public HttpClient CreateClient(string? jwt = null)
    {
        var client = _factory.CreateClient();
        if (!string.IsNullOrWhiteSpace(jwt))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", jwt);
        }

        return client;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();
}

/// <summary>
/// Emisión de JWTs de test con la MISMA firma (secret + issuer) que configura
/// el host de prueba: los tokens pasan la validación JwtBearer del host.
/// </summary>
internal static class TestJwt
{
    public const string Secret = "test-secret-key-at-least-32-characters-long!";
    public const string Issuer = "CoppAddresd.Auth";

    public static string Mint(
        Guid userId,
        string[]? permissions = null,
        Guid? patientId = null,
        string audience = "app")
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
        if (patientId is { } patient)
        {
            claims.Add(new Claim("patient_id", patient.ToString()));
        }

        if (permissions is not null)
        {
            claims.AddRange(permissions.Select(p => new Claim(PermissionClaimTypes.Permission, p)));
        }

        var token = new JwtSecurityToken(
            Issuer, audience, claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

/// <summary>
/// Helpers de seed para los tests de la API: usuarios auth, perfiles de
/// paciente ligados al usuario (base de la resolución del actor), plantillas
/// activas y enrolamientos vía <see cref="ProgramRepository"/>.
/// </summary>
internal static class ProgramApiSeed
{
    private static readonly (TaskCode Code, int Points)[] TaskSeeds =
    [
        (TaskCode.podcast, 80),
        (TaskCode.vitals, 120),
        (TaskCode.nut, 150),
        (TaskCode.ejercicio, 150),
        (TaskCode.nutraceutico, 80),
        (TaskCode.emocional, 120),
    ];

    public static async Task<Guid> CreateAuthUserAsync(AppDbContext db, Guid? id = null)
    {
        var userId = id ?? Guid.NewGuid();
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO auth.\"Users\" (\"Id\") VALUES ({0}) ON CONFLICT DO NOTHING;", userId);
        return userId;
    }

    public static async Task<Guid> CreatePatientAsync(
        AppDbContext db, Guid userId, string? firstName = null)
    {
        var patient = new PatientProfile
        {
            Id = Guid.NewGuid(),
            FirstName = firstName ?? "Ana",
            LastName = "Prueba",
            UserId = userId,
        };
        db.PatientProfiles.Add(patient);
        await db.SaveChangesAsync();
        return patient.Id;
    }

    /// <summary>
    /// Plantilla activa con las 6 tareas × 7 días (código único por llamada
    /// para no chocar con el seed <c>default-83w</c> del host ni entre tests).
    /// </summary>
    public static async Task<Guid> SeedTemplateAsync(
        AppDbContext db, int totalWeeks = 83, string? code = null)
    {
        var template = new ProgramTemplate
        {
            Id = Guid.NewGuid(),
            Code = code ?? $"tpl-{Guid.NewGuid():N}"[..20],
            Name = "Programa 83 semanas",
            TotalWeeks = totalWeeks,
            Status = TemplateStatus.Active,
            Version = 1,
        };

        for (short weekday = 1; weekday <= 7; weekday++)
        {
            for (var i = 0; i < TaskSeeds.Length; i++)
            {
                template.DayTemplates.Add(new WeeklyDayTemplate
                {
                    Weekday = weekday,
                    TaskCode = TaskSeeds[i].Code,
                    Points = TaskSeeds[i].Points,
                    SortOrder = i + 1,
                });
            }
        }

        db.ProgramTemplates.Add(template);
        await db.SaveChangesAsync();
        return template.Id;
    }

    public static async Task<Guid> EnrollAsync(
        AppDbContext db, Guid patientId, Guid templateId, DateOnly start,
        string timezone = "America/Bogota")
    {
        var repo = new ProgramRepository(db, TestConfig());
        var enrollment = await repo.EnrollAsync(patientId, templateId, timezone, start);
        return enrollment.Id;
    }

    public static async Task PauseAsync(AppDbContext db, Guid enrollmentId)
    {
        var repo = new ProgramRepository(db, TestConfig());
        await repo.PauseAsync(enrollmentId);
    }

    public static DateOnly ThisMonday()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
        return today.AddDays(-daysSinceMonday);
    }

    public static IConfiguration TestConfig()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Program:Streak:FreezeGrantEveryPerfectDays"] = "7",
            })
            .Build();

    public static StringContent Json(object payload)
        => new(
            JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            Encoding.UTF8,
            "application/json");
}

// ============================================================================
// T-14 — Autorización & IDOR
// ============================================================================

/// <summary>
/// Tests de autorización y anti-IDOR de la API del programa (SPEC §10.2):
/// AC-11 (paciente A sobre inscripción de paciente B → 404), AC-12 (usuario
/// autenticado sin permiso → 403), estado pausado → 409, sin token → 401,
/// moodScore faltante en emocional → 400 y controles positivos.
/// </summary>
[Collection(ProgramApiTestCollection.Name)]
public sealed class AuthorizationTests(ProgramApiTestDb fixture)
{
    private readonly Lazy<ProgramApiHost> _host =
        new(() => ProgramApiHost.Create(fixture));

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_host.IsValueCreated)
        {
            await _host.Value.DisposeAsync();
        }
    }

    /// <summary>
    /// AC-11: la sesión del paciente A intenta completar una tarea sobre la
    /// inscripción del paciente B → 404 (anti-IDOR), nunca 403 ni 200.
    /// </summary>
    [RequiresPostgresFact]
    public async Task CompleteTask_SobreInscripcionDeOtroPaciente_Devuelve404()
    {
        await using var db = fixture.CreateDbContext();
        var userA = await ProgramApiSeed.CreateAuthUserAsync(db);
        var userB = await ProgramApiSeed.CreateAuthUserAsync(db);
        await ProgramApiSeed.CreatePatientAsync(db, userA, "PacienteA");
        var patientB = await ProgramApiSeed.CreatePatientAsync(db, userB, "PacienteB");
        var templateId = await ProgramApiSeed.SeedTemplateAsync(db);
        var enrollmentB = await ProgramApiSeed.EnrollAsync(db, patientB, templateId, ProgramApiSeed.ThisMonday());

        var client = _host.Value.CreateClient(TestJwt.Mint(userA));

        var response = await client.PostAsync("/api/v1/program/tasks/complete",
            ProgramApiSeed.Json(new
            {
                enrollmentId = enrollmentB,
                localDate = ProgramApiSeed.ThisMonday().AddDays(1),
                taskCode = "podcast",
                clientRequestId = "ac11-1",
            }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// AC-12: usuario autenticado SIN el permiso <c>Program.View</c> → 403 en
    /// un endpoint de clínico (listado de plantillas).
    /// </summary>
    [RequiresPostgresFact]
    public async Task ListTemplates_SinPermisoProgramView_Devuelve403()
    {
        var user = Guid.NewGuid();
        var client = _host.Value.CreateClient(TestJwt.Mint(user));

        var response = await client.GetAsync("/api/v1/program/templates");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Sin token → 401 (requisito [Authorize] del controlador).</summary>
    [RequiresPostgresFact]
    public async Task Snapshot_SinToken_Devuelve401()
    {
        var client = _host.Value.CreateClient();

        var response = await client.GetAsync("/api/v1/program/me/snapshot");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// AC-12 (estado): inscripción pausada → completar tarea devuelve
    /// 409 <c>ENROLLMENT_INACTIVE</c> (SPEC §5.1 y §6.2).
    /// </summary>
    [RequiresPostgresFact]
    public async Task CompleteTask_InscripcionPausada_Devuelve409EnrollmentInactive()
    {
        await using var db = fixture.CreateDbContext();
        var user = await ProgramApiSeed.CreateAuthUserAsync(db);
        var patientId = await ProgramApiSeed.CreatePatientAsync(db, user, "Pausada");
        var templateId = await ProgramApiSeed.SeedTemplateAsync(db);
        var enrollmentId = await ProgramApiSeed.EnrollAsync(db, patientId, templateId, ProgramApiSeed.ThisMonday());
        await ProgramApiSeed.PauseAsync(db, enrollmentId);

        var client = _host.Value.CreateClient(TestJwt.Mint(user));

        var response = await client.PostAsync("/api/v1/program/tasks/complete",
            ProgramApiSeed.Json(new
            {
                enrollmentId,
                localDate = ProgramApiSeed.ThisMonday().AddDays(1),
                taskCode = "podcast",
                clientRequestId = "ac12-1",
            }));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Contains("ENROLLMENT_INACTIVE", doc.RootElement.GetProperty("detail").GetString());
    }

    /// <summary>
    /// Tarea <c>emocional</c> sin <c>moodScore</c> → 400 (validación de forma,
    /// SPEC §3.10 y §6.2; el validador FluentValidation rechaza antes).
    /// </summary>
    [RequiresPostgresFact]
    public async Task CompleteTask_EmocionalSinMoodScore_Devuelve400()
    {
        await using var db = fixture.CreateDbContext();
        var user = await ProgramApiSeed.CreateAuthUserAsync(db);
        var patientId = await ProgramApiSeed.CreatePatientAsync(db, user, "Emocional");
        var templateId = await ProgramApiSeed.SeedTemplateAsync(db);
        var enrollmentId = await ProgramApiSeed.EnrollAsync(db, patientId, templateId, ProgramApiSeed.ThisMonday());

        var client = _host.Value.CreateClient(TestJwt.Mint(user));

        var response = await client.PostAsync("/api/v1/program/tasks/complete",
            ProgramApiSeed.Json(new
            {
                enrollmentId,
                localDate = ProgramApiSeed.ThisMonday().AddDays(1),
                taskCode = "emocional",
                clientRequestId = "mood-1",
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Paciente sin inscripción activa → snapshot 404 NO_ACTIVE_ENROLLMENT.</summary>
    [RequiresPostgresFact]
    public async Task Snapshot_SinInscripcionActiva_Devuelve404()
    {
        await using var db = fixture.CreateDbContext();
        var user = await ProgramApiSeed.CreateAuthUserAsync(db);
        await ProgramApiSeed.CreatePatientAsync(db, user, "SinInscripcion");

        var client = _host.Value.CreateClient(TestJwt.Mint(user));

        var response = await client.GetAsync("/api/v1/program/me/snapshot");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Control positivo: el paciente A completa UNA TAREA PROPIA → 200 con el
    /// shape §7.2 (la guardia anti-IDOR no bloquea al dueño de la inscripción).
    /// </summary>
    [RequiresPostgresFact]
    public async Task CompleteTask_TareaPropia_Devuelve200()
    {
        await using var db = fixture.CreateDbContext();
        var user = await ProgramApiSeed.CreateAuthUserAsync(db);
        var patientId = await ProgramApiSeed.CreatePatientAsync(db, user, "Propia");
        var templateId = await ProgramApiSeed.SeedTemplateAsync(db);
        var enrollmentId = await ProgramApiSeed.EnrollAsync(db, patientId, templateId, ProgramApiSeed.ThisMonday());

        var client = _host.Value.CreateClient(TestJwt.Mint(user));

        var response = await client.PostAsync("/api/v1/program/tasks/complete",
            ProgramApiSeed.Json(new
            {
                enrollmentId,
                localDate = ProgramApiSeed.ThisMonday().AddDays(1),
                taskCode = "podcast",
                clientRequestId = "own-1",
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Control positivo de AC-12: el usuario CON <c>Program.View</c> puede
    /// listar plantillas → 200.
    /// </summary>
    [RequiresPostgresFact]
    public async Task ListTemplates_ConPermisoProgramView_Devuelve200()
    {
        var user = Guid.NewGuid();
        var client = _host.Value.CreateClient(
            TestJwt.Mint(user, permissions: ["Program.View"]));

        var response = await client.GetAsync("/api/v1/program/templates");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}