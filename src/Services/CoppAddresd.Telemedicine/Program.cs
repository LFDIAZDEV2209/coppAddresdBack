using CoppAddresd.Telemedicine.Application;
using CoppAddresd.Telemedicine.Authorization;
using CoppAddresd.Telemedicine.Infrastructure;
using CoppAddresd.Telemedicine.Infrastructure.Extensions;
using CoppAddresd.Telemedicine.Infrastructure.Middleware;
using CoppAddresd.Telemedicine.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);
CoppAddresd.Shared.Security.ErpSessionValidationExtensions.AddErpSessionValidation(builder.Services, builder.Configuration);

// Structured logging (Serilog). Sinks come from the "Serilog" configuration
// section (appsettings.Development.json enables Console + rolling compact-JSON
// file sink "logs/tele-.log"). Enrich.FromLogContext exposes LogContext
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
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()
        )
    );
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title = "CoppAddresd Telemedicina API",
            Version = "v1",
            Description =
                "Microservicio de telemedicina: solicitudes, agendamiento, salas virtuales y encuentros clínicos. Desacoplado del proveedor de video (IVideoProvider).",
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
            Description = "Ingrese el token JWT obtenido del Auth Service",
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
                        Id = JwtBearerDefaults.AuthenticationScheme,
                    },
                },
                Array.Empty<string>()
            },
        }
    );
});

builder.Services.AddTelemedicineApplication(builder.Configuration);
builder.Services.AddTelemedicineInfrastructure(builder.Configuration);

// Modo migración (solo deploy): `dotnet CoppAddresd.Telemedicine.dll --migrate`
// aplica las migraciones pendientes del microservicio (tele.__ef_migrations_history,
// schema tele., aislado del public.__EFMigrationsHistory del backend) y termina
// sin arrancar la API. Lo usa el pipeline de deploy (deploy-backend.yml) con una
// one-off task ECS dentro de la VPC. Idempotente: EF omite lo ya aplicado.
// Va antes de la config de JWT/Twilio a propósito: esta vía solo necesita la BD.
if (args.Contains("--migrate"))
{
    var migrationHost = builder.Build();
    using var migrationScope = migrationHost.Services.CreateScope();
    var migrationDb = migrationScope.ServiceProvider.GetRequiredService<TelemedicineDbContext>();
    await migrationDb.Database.MigrateAsync();
    Console.WriteLine("Migraciones de telemedicina aplicadas correctamente");
    // Flush pendiente antes de terminar el proceso: con un file sink
    // configurado (Serilog:WriteTo) los logs de la migración no se pierden
    // en el tail del run del job de deploy.
    Log.CloseAndFlush();
    return;
}

builder.Services.ConfigureTelemedicineJwt(builder.Configuration);

builder.Services.AddCors(options =>
{
    var origins =
        builder
            .Configuration["Cors:Origins"]
            ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        ?? ["http://localhost:3000", "http://localhost:5080"];

    options.AddPolicy(
        "TelemedicineCors",
        policy => policy.WithOrigins(origins).AllowCredentials().AllowAnyMethod().AllowAnyHeader()
    );
});

builder.Services.AddHealthChecks();

// Modo migración (solo deploy): `dotnet CoppAddresd.Telemedicine.dll --migrate`
// aplica las migraciones pendientes del microservicio (tele.__ef_migrations_history,
// schema tele., aislado del public.__EFMigrationsHistory del backend) y termina
// sin arrancar la API. Lo usa el pipeline de deploy (deploy-backend.yml) con una
// one-off task ECS dentro de la VPC. Idempotente: EF omite lo ya aplicado.
if (args.Contains("--migrate"))
{
    var migrationHost = builder.Build();
    using var migrationScope = migrationHost.Services.CreateScope();
    var migrationDb = migrationScope.ServiceProvider.GetRequiredService<TelemedicineDbContext>();
    await migrationDb.Database.MigrateAsync();
    Console.WriteLine("Migraciones de telemedicina aplicadas correctamente");
    return;
}

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "CoppAddresd Telemedicina API v1");
    });
}

app.UseCors("TelemedicineCors");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Punto de entrada para WebApplicationFactory en pruebas de integración.
public partial class Program;
