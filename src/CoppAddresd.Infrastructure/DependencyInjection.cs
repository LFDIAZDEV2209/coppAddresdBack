using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure.Persistence;
using CoppAddresd.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoppAddresd.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection no configurada.");

        services.AddScoped<AuditTriggerInterceptor>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
            options
                .UseNpgsql(
                    connectionString,
                    npgsql => npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null))
                .AddInterceptors(serviceProvider.GetRequiredService<AuditTriggerInterceptor>()));

        services.AddHttpContextAccessor();
        services.AddScoped<IAuditActorContext, HttpAuditActorContext>();

        return services;
    }
}
