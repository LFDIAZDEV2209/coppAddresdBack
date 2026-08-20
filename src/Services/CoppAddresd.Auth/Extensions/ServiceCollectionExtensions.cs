using System.Security.Claims;
using System.Text;
using CoppAddresd.Auth.Configuration;
using CoppAddresd.Auth.Constants;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using CoppAddresd.Auth.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CoppAddresd.Auth.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuthDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AuthDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql
                    .MigrationsAssembly(typeof(AuthDbContext).Assembly.FullName)
                    // Historial de migraciones aislado en el schema auth (mismo
                    // patrón que Telemedicina con tele.__ef_migrations_history):
                    // public.__EFMigrationsHistory pertenece al backend. Cada
                    // microservicio gestiona su propio historial de migraciones.
                    .MigrationsHistoryTable("__ef_migrations_history", "auth")));

        return services;
    }

    public static IServiceCollection AddAuthIdentity(this IServiceCollection services)
    {
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;
            options.Password.RequiredUniqueChars = 4;

            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            options.User.RequireUniqueEmail = true;

            options.SignIn.RequireConfirmedEmail = false;
        })
        .AddEntityFrameworkStores<AuthDbContext>()
        .AddDefaultTokenProviders();

        return services;
    }

    public static IServiceCollection AddAuthJwt(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSettings = new JwtSettings();
        configuration.GetSection(JwtSettings.SectionName).Bind(jwtSettings);

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;

            // El `aud` del token es el código de la aplicación ("erp", "app").
            // Se aceptan todos los códigos conocidos: el acceso por aplicación
            // se controla en el login vía UserApplication, no en la validación.
            var validAudiences = jwtSettings.ValidAudiences.Count > 0
                ? jwtSettings.ValidAudiences.ToArray()
                : ApplicationCodes.GetAll().ToArray();

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudiences = validAudiences,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            // Validación del security stamp SOLO para la audiencia ERP: la
            // revocación de permisos/roles/desactivación es inmediata para
            // staff/doctores (tráfico bajo, churn alto). La audiencia "app"
            // (pacientes, ~10M) NO valida el stamp por request: la revocación
            // queda sujeta a la expiración natural del token (≤ 15 min), trade-off
            // aceptado para mantener el hot path libre de queries a la BD.
            options.Events.OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                if (principal is null)
                {
                    context.Fail("Security stamp validation failed: no principal");
                    return;
                }

                var audience = principal.Claims
                    .FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Aud)?.Value;

                if (audience != ApplicationCodes.Erp)
                {
                    return;
                }

                var validator = context.HttpContext.RequestServices
                    .GetRequiredService<CoppAddresd.Auth.Security.ISecurityStampValidator>();

                var isValid = await validator.ValidateAsync(principal);
                if (!isValid)
                {
                    context.Fail("Security stamp validation failed");
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                }
            };
        });

        return services;
    }

    public static IServiceCollection AddAuthCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var origins = configuration["Cors:Origins"]
            ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? ["http://localhost:3000"];

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.WithOrigins(origins)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials()
                      // El cliente distingue "sin cookie previa" de "token
                      // inválido" para decidir si muestra el banner de sesión
                      // expirada (header no-safelisted, requiere exposición).
                      .WithExposedHeaders("X-Refresh-Status");
            });
        });

        return services;
    }
}
