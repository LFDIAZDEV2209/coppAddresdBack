using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using CoppAddresd.Api.Authorization;
using CoppAddresd.Api.Context;
using CoppAddresd.Application.Features.ProgramProgress.Commands.LogNutrition;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.Nutrition;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;

namespace CoppAddresd.UnitTests.Program;

/// <summary>
/// Regresión del bug P1: endpoints patient-self-service del módulo Program con
/// autorización de ERP indebida (RequirePermission(Program.View) /
/// [Authorize(Roles="Admin")]) que respondían 403 a los pacientes aud=app
/// (los JWT de la app móvil no llevan claims de permiso ni roles). Los
/// endpoints me/* y nutrition/log resuelven el paciente SIEMPRE del JWT
/// (anti-IDOR AC-11), así que la única gate válida es la autenticación.
/// </summary>
public sealed class ProgramSelfServicePermissionTests
{
    // ===================== Regresión por reflexión =====================

    [Fact]
    public void LogNutrition_NoExigePermiso_PeroMantieneAuthDeControlador()
    {
        var method = GetAction("LogNutrition");

        Assert.Null(FindRequirePermission(method));
        Assert.True(IsControllerAuthorized());
    }

    [Fact]
    public void UpdateNutritionLog_NoExigePermiso()
    {
        var method = GetAction("UpdateNutritionLog");

        Assert.Null(FindRequirePermission(method));
        Assert.True(IsControllerAuthorized());
    }

    [Fact]
    public void RecordWeight_MantieneSuGateDeliberadoDeRolAdmin()
    {
        // NO es outlier: el [Authorize(Roles="Admin")] de me/weight es una
        // decisión de producto cubierta por WeightAuthorizationTests
        // (OnlyExistingGlobalAdminCanReachWeightWrite): Patient/ClinicAdmin →
        // 403, solo el Admin global escribe peso en self-service. El registro
        // con identidad ERP vive en erp/biometria (RequirePermission).
        var method = GetAction("RecordWeight");

        var authorize = method.GetCustomAttributes(true).OfType<AuthorizeAttribute>().ToList();
        Assert.Contains(authorize, a => a.Roles is not null && a.Roles.Contains("Admin"));
        Assert.True(IsControllerAuthorized());
    }

    [Fact]
    public void EndpointsErpDelModulo_ConservanSusPermisos()
    {
        // El fix NO debe abrir el alcance: los endpoints ERP/staff siguen
        // exigiendo permiso granular (no tocar).
        Assert.NotNull(FindRequirePermission(GetAction("ListEnrollments")));
        Assert.NotNull(FindRequirePermission(GetAction("CalculateScores")));
        Assert.NotNull(FindRequirePermission(GetAction("GetErpDashboard")));
    }

    private static bool IsControllerAuthorized() =>
        typeof(CoppAddresd.Api.Controllers.ProgramController)
            .GetCustomAttributes(true)
            .OfType<AuthorizeAttribute>()
            .Any();

    private static System.Reflection.MethodInfo GetAction(string name) =>
        typeof(CoppAddresd.Api.Controllers.ProgramController)
            .GetMethods()
            .Single(m => m.Name == name);

    private static RequirePermissionAttribute? FindRequirePermission(
        System.Reflection.MethodInfo method
    ) => method.GetCustomAttributes(true).OfType<RequirePermissionAttribute>().FirstOrDefault();

    // ===================== Regresión de pipeline HTTP =====================

    /// <summary>Actor fijo: el "paciente sin roles" del escenario del bug.</summary>
    private sealed class StubProgramActorContext : IProgramActorContext
    {
        public static readonly Guid TestPatientId = Guid.NewGuid();
        public static readonly Guid TestEnrollmentId = Guid.NewGuid();
        public static readonly Guid TestUserId = Guid.NewGuid();

        public Guid? UserId => TestUserId;

        public IReadOnlyList<string> Roles => [];

        public Task<Guid?> ResolvePatientProfileIdAsync(CancellationToken ct = default) =>
            Task.FromResult<Guid?>(TestPatientId);

        public Task<Guid?> ResolveActiveEnrollmentIdAsync(CancellationToken ct = default) =>
            Task.FromResult<Guid?>(TestEnrollmentId);

        public Task<bool> EnrollmentBelongsToCurrentPatientAsync(
            Guid enrollmentId,
            CancellationToken ct = default
        ) => Task.FromResult(enrollmentId == TestEnrollmentId);

        public Task<bool> ActorScopedToEnrollmentAsync(
            Guid enrollmentId,
            CancellationToken ct = default
        ) => Task.FromResult(enrollmentId == TestEnrollmentId);

        public Task<IReadOnlyList<Guid>?> ResolveScopedPatientIdsAsync(
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyList<Guid>?>([TestPatientId]);
    }

    private static WebApplicationFactory<CoppAddresd.Api.ApiEntryPoint> NewFactory()
    {
        // Stub del repositorio: agua acumula (200), comidas dupliquean 409.
        var loggedMeals = new HashSet<(MealCode Code, DateOnly Date)>();
        var repository = Substitute.For<IProgramRepository>();
        repository
            .LogNutritionAsync(
                Arg.Any<Guid>(),
                Arg.Any<MealCode>(),
                Arg.Any<DateOnly?>(),
                Arg.Any<NutritionIntakePayload?>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(callInfo =>
            {
                var mealCode = callInfo.ArgAt<MealCode>(1);
                var date = callInfo.ArgAt<DateOnly?>(2) ?? DateOnly.FromDateTime(DateTime.UtcNow);

                if (mealCode == MealCode.agua)
                {
                    // El agua acumula: repetición → 200 con XP 0 (SPEC §18).
                    return new NutritionLogResultDto(Guid.NewGuid(), "agua", date, true, 0, 42);
                }

                if (!loggedMeals.Add((mealCode, date)))
                {
                    throw new BusinessRuleViolationException("HABIT_ALREADY_LOGGED");
                }

                return new NutritionLogResultDto(
                    Guid.NewGuid(),
                    mealCode.ToString(),
                    date,
                    true,
                    5,
                    47
                );
            });

        var actorContext = Substitute.For<IProgramActorContext>();
        actorContext
            .ResolvePatientProfileIdAsync(Arg.Any<CancellationToken>())
            .Returns(StubProgramActorContext.TestPatientId);
        actorContext.UserId.Returns(StubProgramActorContext.TestUserId);

        return new WebApplicationFactory<CoppAddresd.Api.ApiEntryPoint>().WithWebHostBuilder(
            builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting(
                    "Storage:SignatureKey",
                    "test-signature-key-32-chars-long-for-ci"
                );
                builder.UseSetting(
                    "ConnectionStrings:DefaultConnection",
                    "Host=localhost;Database=coppaddresd_test;Username=test;Password=test;Port=5432"
                );
                builder.UseSetting("Jwt:Secret", "test-jwt-secret-32-chars-long-for-ci-xyz123");
                builder.UseSetting("Jwt:Issuer", "CoppAddresd.Auth");
                builder.UseSetting("SkipDatabaseInitialization", "true");
                builder.ConfigureServices(services =>
                {
                    // El paciente self-service no toca la BD del programa: el
                    // repositorio es el stub y el actor se resuelve del JWT.
                    var programRepository = services.First(d =>
                        d.ServiceType == typeof(IProgramRepository)
                    );
                    services.Remove(programRepository);
                    services.AddScoped(_ => repository);

                    var programActor = services.First(d =>
                        d.ServiceType == typeof(IProgramActorContext)
                    );
                    services.Remove(programActor);
                    services.AddScoped(_ => actorContext);
                });
            }
        );
    }

    /// <summary>JWT aud=app SIN claims de permiso y SIN roles (el caso del bug).</summary>
    private static string MakePatientToken()
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("test-jwt-secret-32-chars-long-for-ci-xyz123")
        );
        var token = new JwtSecurityToken(
            issuer: "CoppAddresd.Auth",
            audience: "app",
            claims: [new Claim("sub", StubProgramActorContext.TestUserId.ToString())],
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task LogNutrition_PacienteSinRoles_Agua200_YAguaRepetida200()
    {
        await using var factory = NewFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            MakePatientToken()
        );

        var first = await client.PostAsJsonAsync(
            "/api/v1/program/nutrition/log",
            new { mealCode = "agua", localDate = (string?)null }
        );
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // La repetición del agua acumula: 200 con xpAwarded = 0 (diseño SPEC §18).
        var repeat = await client.PostAsJsonAsync(
            "/api/v1/program/nutrition/log",
            new { mealCode = "agua", localDate = (string?)null }
        );
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        var payload = await repeat.Content.ReadFromJsonAsync<NutritionLogResultDto>();
        Assert.NotNull(payload);
        Assert.Equal(0, payload!.XpAwarded);
    }

    [Fact]
    public async Task LogNutrition_PacienteSinRoles_ComidaDuplicada_409()
    {
        await using var factory = NewFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            MakePatientToken()
        );

        var first = await client.PostAsJsonAsync(
            "/api/v1/program/nutrition/log",
            new { mealCode = "des", localDate = (string?)null }
        );
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // El duplicado de una COMIDA el mismo día → 409 HABIT_ALREADY_LOGGED
        // (ProblemDetails del middleware global de excepciones).
        var duplicate = await client.PostAsJsonAsync(
            "/api/v1/program/nutrition/log",
            new { mealCode = "des", localDate = (string?)null }
        );
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }
}
