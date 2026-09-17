using System.Text;
using CoppAddresd.Api.Authorization;
using CoppAddresd.Api.Configuration;
using CoppAddresd.Api.Context;
using CoppAddresd.Api.Handlers;
using CoppAddresd.Api.Security;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Common.Behaviors;
using CoppAddresd.Application.Features.FoodAi;
using CoppAddresd.Application.Features.Media;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Services.ProgramProgress;
using CoppAddresd.Infrastructure.Extensions;
using CoppAddresd.Infrastructure.Persistence;
using CoppAddresd.Infrastructure.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

namespace CoppAddresd.Api.Extensions;

public static class ApplicationServiceExtensions
{
    public const string CorsPolicyName = "AllowAll";

    public static IServiceCollection AddCoppAddresdApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<AiServiceSettings>(
            configuration.GetSection(AiServiceSettings.SectionName)
        );

        // Configuración de Firebase Cloud Messaging (pushes a dispositivos).
        services.Configure<FcmSettings>(
            configuration.GetSection(FcmSettings.SectionName));

        services.Configure<FoodAiSettings>(
            configuration.GetSection(FoodAiSettings.SectionName));

        // Controles proactivos del programa (días 7/14/21/45/60/90):
        // configuración + proveedor de plantillas (v1 estáticas por
        // configuración; el proveedor con LLM futuro se enchufa detrás de la
        // misma interfaz sin tocar el scheduler) + notificador que reutiliza
        // SendPushNotificationCommand (FCM + inyección proactiva en el chat) +
        // job orquestador (lo consume ProgramControlHostedService).
        services.Configure<ProgramControlSettings>(
            configuration.GetSection(ProgramControlSettings.SectionName));
        services.AddScoped<IProgramControlTemplateProvider, PredefinedProgramControlTemplateProvider>();
        services.AddScoped<IProgramControlNotifier, ProgramControlNotifier>();
        services.AddScoped<ProgramControlJob>();

        // Clave interna compartida con el microservicio de Telemedicina
        // (endpoints /api/v1/internal/telemedicine, header X-Internal-Key).
        services.Configure<TelemedicineServiceSettings>(
            configuration.GetSection(TelemedicineServiceSettings.SectionName)
        );

        // Canal "app/community" de las notificaciones de alertas de tests
        // (SPEC A13): cliente tipado hacia el endpoint interno de mensajería
        // del microservicio de Comunidad. La clave interna viaja por request
        // (header X-Internal-Key) desde CommunityMessageSender.
        services.Configure<CommunityServiceSettings>(
            configuration.GetSection(CommunityServiceSettings.SectionName)
        );
        services
            .AddHttpClient<ICommunityMessageSender, CommunityMessageSender>(
                (sp, client) =>
                {
                    var communitySettings = sp
                        .GetRequiredService<IOptions<CommunityServiceSettings>>()
                        .Value;
                    client.BaseAddress = new Uri(communitySettings.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(communitySettings.TimeoutSeconds);
                }
            )
            .AddResiliencePolicy()
            .AddHttpMessageHandler<CorrelationIdDelegatingHandler>();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(
                typeof(CoppAddresd.Application.Features.Chat.ChatCommand).Assembly
            );
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(CreateMediaItemCommand).Assembly);

        services
            .AddHttpClient<IAiServiceClient, AiServiceClient>()
            .AddResiliencePolicy()
            .AddHttpMessageHandler<CorrelationIdDelegatingHandler>();

services.AddHttpClient<IFoodAiClient, FoodAiClient>()
            .AddHttpMessageHandler<CorrelationIdDelegatingHandler>();

        services.AddScoped<IImageStorage, LocalImageStorage>();
        services.AddScoped<CoppAddresd.Application.Features.FoodAi.ImageFileValidator>();
        services.AddScoped<INutritionProvider, DatabaseNutritionProvider>();
        services.AddScoped<INutritionCalculator, NutritionCalculator>();
        services.AddScoped<IFoodAnalysisRepository, FoodAnalysisRepository>();

        services.AddHttpClient<IAgentRuntimeSyncService, AgentRuntimeSyncService>((sp, client) =>
        {
            var aiSettings = sp.GetRequiredService<IOptions<AiServiceSettings>>().Value;
            client.BaseAddress = new Uri(aiSettings.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(aiSettings.TimeoutSeconds);
        }).AddHttpMessageHandler<CorrelationIdDelegatingHandler>();

        // FCM: mismo patrón que AiServiceClient (HttpClient tipado). El
        // cliente degrada a "disabled" sin credenciales, nunca lanza.
        services.AddHttpClient<IFcmClient, FcmClient>()
            .AddResiliencePolicy()
            .AddHttpMessageHandler<CorrelationIdDelegatingHandler>();

        services
            .AddHttpClient<IAgentRuntimeSyncService, AgentRuntimeSyncService>(
                (sp, client) =>
                {
                    var aiSettings = sp.GetRequiredService<IOptions<AiServiceSettings>>().Value;
                    client.BaseAddress = new Uri(aiSettings.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(aiSettings.TimeoutSeconds);
                }
            )
            .AddHttpMessageHandler<CorrelationIdDelegatingHandler>();

        services
            .AddHttpClient<IAgentExecutionsQueryService, AgentExecutionsQueryService>(
                (sp, client) =>
                {
                    var aiSettings = sp.GetRequiredService<IOptions<AiServiceSettings>>().Value;
                    client.BaseAddress = new Uri(aiSettings.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(aiSettings.TimeoutSeconds);
                }
            )
            .AddHttpMessageHandler<CorrelationIdDelegatingHandler>();

        // Introspección de permisos scoped hacia el Auth Service.
        services.AddScoped<IProfessionalAccessProjectionRepository, CoppAddresd.Infrastructure.Repositories.ProfessionalAccessProjectionRepository>();
        services.AddHostedService<ErpAccessProjectionWorker>();
        services.AddHttpClient<IErpAccessClient, ErpAccessClient>((sp, client) =>
        {
            var settings = sp.GetRequiredService<IOptions<AuthServiceSettings>>().Value;
            client.BaseAddress = new Uri(settings.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
            client.DefaultRequestHeaders.Add("X-Internal-Key", settings.InternalApiKey);
        });
        services.Configure<AuthServiceSettings>(
            configuration.GetSection(AuthServiceSettings.SectionName)
        );
        services
            .AddHttpClient<IScopedAuthorizationClient, ScopedAuthorizationClient>(
                (sp, client) =>
                {
                    var authSettings = sp.GetRequiredService<IOptions<AuthServiceSettings>>().Value;
                    client.BaseAddress = new Uri(authSettings.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(authSettings.TimeoutSeconds);
                    client.DefaultRequestHeaders.Add("X-Internal-Key", authSettings.InternalApiKey);
                }
            )
            .AddResiliencePolicy();

        services
            .AddHttpClient<IAuthInvitationsClient, AuthInvitationsClient>(
                (sp, client) =>
                {
                    var authSettings = sp.GetRequiredService<IOptions<AuthServiceSettings>>().Value;
                    client.BaseAddress = new Uri(authSettings.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(authSettings.TimeoutSeconds);
                    client.DefaultRequestHeaders.Add("X-Internal-Key", authSettings.InternalApiKey);
                }
            )
            .AddResiliencePolicy();

        services
            .AddHttpClient<IAuthScopedAssignmentsClient, AuthScopedAssignmentsClient>(
                (sp, client) =>
                {
                    var authSettings = sp.GetRequiredService<IOptions<AuthServiceSettings>>().Value;
                    client.BaseAddress = new Uri(authSettings.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(authSettings.TimeoutSeconds);
                    client.DefaultRequestHeaders.Add("X-Internal-Key", authSettings.InternalApiKey);
                }
            )
            .AddResiliencePolicy();

        services
            .AddHttpClient<IAuthUsersByRoleClient, AuthUsersByRoleClient>(
                (sp, client) =>
                {
                    var authSettings = sp.GetRequiredService<IOptions<AuthServiceSettings>>().Value;
                    client.BaseAddress = new Uri(authSettings.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(authSettings.TimeoutSeconds);
                    client.DefaultRequestHeaders.Add("X-Internal-Key", authSettings.InternalApiKey);
                }
            )
            .AddResiliencePolicy();

        // Consulta de roles por nombre (resolución del rol "Professional" para
        // la sincronización automática de scopes por clínica).
        services
            .AddHttpClient<IAuthRolesClient, AuthRolesClient>(
                (sp, client) =>
                {
                    var authSettings = sp.GetRequiredService<IOptions<AuthServiceSettings>>().Value;
                    client.BaseAddress = new Uri(authSettings.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(authSettings.TimeoutSeconds);
                    client.DefaultRequestHeaders.Add("X-Internal-Key", authSettings.InternalApiKey);
                }
            )
            .AddResiliencePolicy();

        services.AddScoped<ICurrentContext, CurrentContext>();

        // Resolución del actor del módulo Progreso del Programa (SPEC §6.14 y
        // PLAN OQ-1): paciente derivado del JWT (claim patient_id o lookup por
        // app.patient_profiles.user_id), memoizado por request.
        services.AddScoped<IProgramActorContext, ProgramActorContext>();

        return services;
    }

    public static IServiceCollection ConfigureCors(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var origins =
            configuration["Cors:Origins"]
                ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? ["http://localhost:3000", "http://localhost:5080"];

        services.AddCors(options =>
        {
            options.AddPolicy(
                CorsPolicyName,
                policy =>
                {
                    policy
                        .WithOrigins(origins)
                        .AllowCredentials()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                }
            );
        });

        return services;
    }

    public static IServiceCollection ConfigureJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var jwtSettings = configuration.GetSection("Jwt");
        var secret = jwtSettings["Secret"]!;
        var issuer = jwtSettings["Issuer"]!;

        // El `aud` del token es el código de la aplicación ("erp", "app").
        // Esta API sirve endpoints para ambas aplicaciones, por lo que acepta
        // todos los códigos conocidos; si no se configuran, se asume la lista
        // de aplicaciones actuales.
        var validAudiences =
            jwtSettings.GetSection("ValidAudiences").Get<string[]>() ?? ["erp", "app"];

        services
            .AddAuthentication(options =>
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
                    ClockSkew = TimeSpan.Zero,
                };
            });

        services.AddAuthorization();

        // Autorización por permisos (claims) con política por código de permiso:
        // [RequirePermission("Patients.View")] sin registrar cada política.
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();

        return services;
    }
}
