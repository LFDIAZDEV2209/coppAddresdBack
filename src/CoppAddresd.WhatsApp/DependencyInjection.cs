using CoppAddresd.WhatsApp.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoppAddresd.WhatsApp;

public static class DependencyInjection
{
    public static IServiceCollection AddWhatsApp(this IServiceCollection services, IConfiguration configuration,
        bool migrationsOnly = false, WhatsAppOptions? options = null)
    {
        options ??= WhatsAppOptions.FromEnvironment();
        if (!options.Enabled && !migrationsOnly) return services;
        if (!migrationsOnly) options.Validate();
        services.AddDbContext<WhatsAppDbContext>(db => db.UseNpgsql(
            configuration.GetConnectionString("DefaultConnection"),
            pg => pg.MigrationsHistoryTable("__ef_migrations_history", "whatsapp")));
        if (migrationsOnly) return services;
        services.AddSingleton(options);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<Webhook.WhatsAppQueue>();
        services.AddScoped<Webhook.WhatsAppWebhook>();
        services.AddHttpClient<CoppAddresd.Application.WhatsApp.Messaging.IWhatsAppMessageSender,
            Messaging.MetaCloudWhatsAppSender>().RemoveAllLoggers();
        return services;
    }
}
