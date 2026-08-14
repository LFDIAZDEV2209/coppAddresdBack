using System.Text;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Common.Behaviors;
using CoppAddresd.Application.Features.Media;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure.Extensions;
using CoppAddresd.Infrastructure.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

namespace CoppAddresd.Api.Extensions;

public static class ApplicationServiceExtensions
{
    public const string CorsPolicyName = "AllowAll";

    public static IServiceCollection AddCoppAddresdApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AiServiceSettings>(
            configuration.GetSection(AiServiceSettings.SectionName));

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(CoppAddresd.Application.Features.Chat.ChatCommand).Assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(CreateMediaItemCommand).Assembly);

        services.AddHttpClient<IAiServiceClient, AiServiceClient>()
            .AddResiliencePolicy();

        services.AddHttpClient<IAgentRuntimeSyncService, AgentRuntimeSyncService>();

        return services;
    }

    public static IServiceCollection ConfigureCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var origins = configuration["Cors:Origins"]
            ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? ["http://localhost:3000"];

        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                policy.WithOrigins(origins)
                      .AllowCredentials()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        return services;
    }

    public static IServiceCollection ConfigureJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("Jwt");
        var secret = jwtSettings["Secret"]!;
        var issuer = jwtSettings["Issuer"]!;

        // El `aud` del token es el código de la aplicación ("erp", "app").
        // Esta API sirve endpoints para ambas aplicaciones, por lo que acepta
        // todos los códigos conocidos; si no se configuran, se asume la lista
        // de aplicaciones actuales.
        var validAudiences = jwtSettings.GetSection("ValidAudiences").Get<string[]>()
            ?? ["erp", "app"];

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudiences = validAudiences,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });

        services.AddAuthorization();

        return services;
    }
}
