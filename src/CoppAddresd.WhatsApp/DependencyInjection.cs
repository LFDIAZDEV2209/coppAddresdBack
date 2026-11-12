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
        services.AddScoped<Messaging.WhatsAppPatientResolver>();
        services.AddScoped<Messaging.WhatsAppInboundProcessor>();
        var auth = configuration.GetSection("WhatsApp:Auth").Get<Binding.WhatsAppAuthOptions>() ?? new();
        var authBase = auth.GetBaseUri();
        services.AddScoped<Binding.WhatsAppBindingFlow>();
        services.AddHttpClient<CoppAddresd.Application.WhatsApp.Binding.IWhatsAppOtpClient, Binding.AuthOtpClient>(client =>
        {
            client.BaseAddress = authBase;
            client.Timeout = TimeSpan.FromSeconds(10);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false })
            .RemoveAllLoggers();
        services.AddHostedService<Webhook.WhatsAppInboundWorker>();
        var reminders = configuration.GetSection("WhatsApp:Reminders").Get<Reminders.ReminderSettings>() ?? new();
        reminders.Validate();
        services.AddSingleton(reminders);
        services.AddScoped<CoppAddresd.Application.WhatsApp.IWhatsAppReminderHooks, Reminders.WhatsAppReminderHooks>();
        services.AddScoped<Reminders.WhatsAppReminderScheduler>();
        services.AddScoped<Reminders.WhatsAppReminderProcessor>();
        if (reminders.Enabled) services.AddHostedService<Reminders.WhatsAppReminderWorker>();
        services.AddHttpClient<CoppAddresd.Application.WhatsApp.Messaging.IWhatsAppMessageSender,
            Messaging.MetaCloudWhatsAppSender>().RemoveAllLoggers();
        return services;
    }
}
