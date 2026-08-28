using CoppAddresd.Api.Authorization;
using CoppAddresd.Api.Extensions;
using CoppAddresd.Api.Middleware;
using CoppAddresd.Api.Security;
using CoppAddresd.Api.Seeders;
using CoppAddresd.Infrastructure;
using CoppAddresd.Infrastructure.HealthChecks;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Prohibido un valor por defecto de firma en el código: la clave debe venir
// de configuración segura (appsettings gitignoreado / variables de entorno /
// secrets manager). Sin ella, falla al arrancar (fail-fast, sin secretos en
// el repositorio).
var storageSignatureKey = builder.Configuration["Storage:SignatureKey"]
    ?? throw new InvalidOperationException(
        "Storage:SignatureKey no configurado. Define el secreto en appsettings o variables de entorno.");

builder.Services.AddSingleton(new StorageSignatureService(storageSignatureKey));

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

// Seed de reglas de seguridad clínica para la generación de planes con IA.
builder.Services.AddHostedService<ClinicalSafetyRulesSeeder>();

// Seed de la plantilla por defecto del programa de 83 semanas (default-83w).
builder.Services.AddHostedService<ProgramProgressSeeder>();

// Seed de rutinas de ejercicio base para el configurador de contenido del ERP.
builder.Services.AddHostedService<ExerciseRoutineSeeder>();

// Health check de conectividad con PostgreSQL. AddDbContextCheck requiere el
// paquete Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore
// (no incluido en el shared framework de .NET 10), así que se usa un check
// propio con CanConnectAsync: sin dependencias NuGet adicionales.
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CoppAddresd API",
        Version = "v1",
        Description = "API principal de CoppAddresd"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingrese el token JWT obtenido del Auth service"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

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
