using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.WhatsApp.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace CoppAddresd.Infrastructure.Sos;

public static class SosWhatsAppRegistration
{
    public static IServiceCollection AddSosWhatsApp(this IServiceCollection services,IConfiguration config)
    {
        services.AddOptions<SosWhatsAppSettings>().Bind(config.GetSection("SosWhatsApp"))
            .Validate(s=>!s.Enabled || System.Text.RegularExpressions.Regex.IsMatch(s.TemplateName,"^[a-z0-9_]{1,100}$") && s.Language=="es", "Configura plantilla SOS aprobada en español.").ValidateOnStart();
        services.AddScoped<SosWhatsAppProcessor>();
        services.AddScoped<ISosWhatsAppStatusReceiver>(sp=>sp.GetRequiredService<SosWhatsAppProcessor>());
        if(config.GetValue<bool>("SosWhatsApp:Enabled")) services.AddHostedService<SosWhatsAppWorker>();
        return services;
    }
}
public sealed class SosWhatsAppWorker(IServiceScopeFactory scopes,ILogger<SosWhatsAppWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while(!ct.IsCancellationRequested) {
            try {
                using var scope=scopes.CreateScope();
                // Si WhatsApp global está apagado, deja el outbox intacto.
                if(scope.ServiceProvider.GetService<IWhatsAppMessageSender>() is not null &&
                    await scope.ServiceProvider.GetRequiredService<SosWhatsAppProcessor>().ProcessNextAsync(ct))continue;
            } catch(OperationCanceledException) when(ct.IsCancellationRequested){break;}
            catch(Exception ex){logger.LogWarning("Worker SOS WhatsApp: {FailureType}",ex.GetType().Name);}
            await Task.Delay(TimeSpan.FromSeconds(2),ct);
        }
    }
}
