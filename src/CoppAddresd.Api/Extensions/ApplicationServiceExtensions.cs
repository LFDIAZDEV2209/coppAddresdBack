using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure.Extensions;
using CoppAddresd.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

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
        });

        services.AddHttpClient<IAiServiceClient, AiServiceClient>()
            .AddResiliencePolicy();

        return services;
    }

    public static IServiceCollection ConfigureCors(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                policy
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });

        return services;
    }
}
