using System.Text.Json.Serialization;
using CoppAddresd.Api.Authorization;
using CoppAddresd.Api.BackgroundJobs;
using CoppAddresd.Api.Extensions;
using CoppAddresd.Api.Middleware;
using CoppAddresd.Api.Security;
using CoppAddresd.Api.Seeders;
using CoppAddresd.Infrastructure;
using CoppAddresd.Infrastructure.HealthChecks;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);
CoppAddresd.Shared.Security.ErpSessionValidationExtensions.AddErpSessionValidation(builder.Services, builder.Configuration);

// Structured logging (Serilog). Sinks come from the "Serilog" configuration
// section (appsettings.Development.json enables Console + rolling compact-JSON
// file sink "logs/api-.log"). Enrich.FromLogContext exposes LogContext
// properties (CorrelationId) on every event. When no Serilog:WriteTo section
// is configured (non-Development environments) it falls back to console-only,
// preserving the previous default console logging behavior: Information for
// app code, Microsoft.AspNetCore lowered to Warning (same verbosity as
// appsettings.Example.json Logging:LogLevel), and a template that includes
// {Properties:j} so CorrelationId/RequestPath stay visible on console.
builder.Host.UseSerilog((ctx, cfg) =>
{
    cfg.ReadFrom.Configuration(ctx.Configuration).Enrich.FromLogContext();

    if (!ctx.Configuration.GetSection("Serilog:WriteTo").GetChildren().Any())
    {
        cfg.MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
    }
});

builder
    .Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter())
    );

// Prohibido un valor por defecto de firma en el código: la clave debe venir
// de configuración segura (appsettings gitignoreado / variables de entorno /
// secrets manager). Sin ella, falla al arrancar (fail-fast, sin secretos en
// el repositorio). Se omite en modo --migrate: esa vía solo necesita la BD.
if (!args.Contains("--migrate"))
{
    var storageSignatureKey =
        builder.Configuration["Storage:SignatureKey"]
        ?? throw new InvalidOperationException(
            "Storage:SignatureKey no configurado. Define el secreto en appsettings o variables de entorno."
        );

    builder.Services.AddSingleton(new StorageSignatureService(storageSignatureKey));
}

builder.Services.AddCoppAddresdApplicationServices(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.ConfigureCors(builder.Configuration);
builder.Services.ConfigureJwtAuthentication(builder.Configuration);
builder.Services.AddHttpContextAccessor();

// Handler de propagación de correlation ID: AddHttpMessageHandler<T> resuelve
// el handler desde DI (obligatorio registrarlo explícitamente).
builder.Services.AddTransient<CoppAddresd.Api.Handlers.CorrelationIdDelegatingHandler>();

// Seed del catálogo de agentes (idempotente) + sync al AI Service al arrancar.
builder.Services.AddHostedService<AgentCatalogSeeder>();

// Seed del catálogo de mediciones clínicas (unidades, métricas y rangos).
builder.Services.AddHostedService<ClinicalMeasurementsSeeder>();

// Seed de Biometría: mediciones clínicas (weight/height/waist/hip/wrist) y datos demográficos.
builder.Services.AddHostedService<BiometriaSeeder>();

// Seed de reglas de seguridad clínica para la generación de planes con IA.
builder.Services.AddHostedService<ClinicalSafetyRulesSeeder>();

// Seed del catálogo nutricional de Food AI (schema foodai, USDA FDC).
builder.Services.AddHostedService<FoodAiNutritionSeeder>();

// Seed de la plantilla por defecto del programa de 83 semanas (default-83w).
builder.Services.AddHostedService<ProgramProgressSeeder>();

// Seed de rutinas de ejercicio base para el configurador de contenido del ERP.
builder.Services.AddHostedService<ExerciseRoutineSeeder>();

// Seed de desarrollo: inscribe masivamente a más de 20 pacientes en default-83w,
// asigna planes nutricionales, rutinas de ejercicio por día de semana y simula
// progreso histórico y XP en tiers realistas. Se registra DESPUÉS de
// ProgramProgressSeeder y ExerciseRoutineSeeder.
builder.Services.AddHostedService<DevProgramSeeder>();

// Backfill y reconciliación histórica de métricas CQRS (puebla rollups para datos existentes)
builder.Services.AddSingleton<MetricsBackfillSeeder>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MetricsBackfillSeeder>());

// Reconciliación nocturna de rachas (B12, T-28): job diario configurable vía
// Program:Reconciliation (Enabled/HourUtc); disparo manual en
// POST /program/maintenance/reconcile-streaks.
builder.Services.AddHostedService<ReconcileStreakHostedService>();

// Controles proactivos del programa (días 7/14/21/45/60/90):
// job periódico configurable vía Program:Controls (Enabled, TickMinutes,
// ventana local, plantillas). Push FCM + mensaje proactivo en el chat.
builder.Services.AddHostedService<ProgramControlHostedService>();

// Health check de conectividad con PostgreSQL. AddDbContextCheck requiere el
// paquete Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore
// (no incluido en el shared framework de .NET 10), así que se usa un check
// propio con CanConnectAsync: sin dependencias NuGet adicionales.
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title = "CoppAddresd API",
            Version = "v1",
            Description = "API principal de CoppAddresd",
        }
    );

    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Ingrese el token JWT obtenido del Auth service",
        }
    );

    options.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer",
                    },
                },
                Array.Empty<string>()
            },
        }
    );
});

// Modo migración (solo deploy): `dotnet CoppAddresd.Api.dll --migrate` aplica
// las migraciones pendientes del backend (public.__EFMigrationsHistory, incluye
// el seed de catálogos de AddServerCatalogSeeds) y termina sin arrancar la API.
// Lo usa el pipeline de deploy (deploy-backend.yml) con una one-off task de ECS
// dentro de la VPC: el runner de CI no alcanza el RDS privado, y el task def de
// la API ya trae la conexión (ConnectionStrings__DefaultConnection). Idempotente:
// EF omite las migraciones ya aplicadas.
if (args.Contains("--migrate"))
{
    var migrationHost = builder.Build();
    using var migrationScope = migrationHost.Services.CreateScope();
    var migrationDb = migrationScope.ServiceProvider.GetRequiredService<AppDbContext>();
    await migrationDb.Database.MigrateAsync();
    Console.WriteLine("Migraciones del backend aplicadas correctamente");
    // Flush pendiente antes de terminar el proceso: con un file sink
    // configurado (Serilog:WriteTo) los logs de la migración no se pierden
    // en el tail del run del job de deploy.
    Log.CloseAndFlush();
    return;
}

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.All
});

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "CoppAddresd API v1");
    });
}

app.UseCors(ApplicationServiceExtensions.CorsPolicyName);
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Clase pública del entry point: requerida por WebApplicationFactory en los
// tests de integración de la API (B5, T-14/T-15).
public partial class Program;
