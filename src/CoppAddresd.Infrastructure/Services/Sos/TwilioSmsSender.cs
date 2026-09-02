using System.Xml;
using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace CoppAddresd.Infrastructure.Services.Sos;

/// <summary>
/// Configuración de Twilio específica para SOS (SMS + voz).
/// </summary>
public sealed class TwilioSosOptions
{
    public const string SectionName = "Twilio";

    /// <summary>SID de la cuenta Twilio.</summary>
    public string AccountSid { get; set; } = string.Empty;

    /// <summary>API Key (username para TwilioClient.Init).</summary>
    public string ApiKeySid { get; set; } = string.Empty;

    /// <summary>API Secret (password para TwilioClient.Init).</summary>
    public string ApiKeySecret { get; set; } = string.Empty;

    /// <summary>Número de envío Twilio (E.164, ej. "+1234567890").</summary>
    public string FromNumber { get; set; } = string.Empty;

    /// <summary>Si false, el servicio no envía SMS reales (guardia de seguridad).</summary>
    public bool IsEnabled { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AccountSid)
        && !string.IsNullOrWhiteSpace(ApiKeySid)
        && !string.IsNullOrWhiteSpace(ApiKeySecret)
        && !string.IsNullOrWhiteSpace(FromNumber);
}

/// <summary>
/// Envío de SMS reales vía Twilio. Inicializa el SDK con
/// <c>TwilioClient.Init(ApiKeySid, ApiKeySecret, AccountSid)</c> (orden
/// específico: username, password, accountSid). NUNCA usa SetRegion.
/// </summary>
public sealed class TwilioSmsSender(
    IOptions<TwilioSosOptions> options,
    ILogger<TwilioSmsSender> logger) : ISmsSender
{
    public async Task SendAsync(SmsMessage message, CancellationToken ct = default)
    {
        var opts = options.Value;

        if (!opts.IsEnabled || !opts.IsConfigured)
        {
            logger.LogWarning("TwilioSmsSender: deshabilitado o sin configurar. SMS a {To} omitido.", message.To);
            throw new InvalidOperationException("Twilio SMS no configurado o deshabilitado.");
        }

        // Inicialización con las credenciales de la API Key (orden específico).
        TwilioClient.Init(opts.ApiKeySid, opts.ApiKeySecret, opts.AccountSid);

        try
        {
            var resource = await MessageResource.CreateAsync(
                body: message.Body,
                from: new Twilio.Types.PhoneNumber(opts.FromNumber),
                to: new Twilio.Types.PhoneNumber(message.To));

            logger.LogInformation(
                "SMS enviado vía Twilio: to={To}, sid={Sid}, status={Status}",
                message.To, resource.Sid, resource.Status);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al enviar SMS vía Twilio a {To}.", message.To);
            throw;
        }
    }
}

/// <summary>
/// Fake de SMS que solo loguea (desarrollo, sin credenciales de Twilio).
/// </summary>
public sealed class LogSmsSender(ILogger<LogSmsSender> logger) : ISmsSender
{
    public Task SendAsync(SmsMessage message, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[SOS-SMS-LOG] To: {To}\nBody: {Body}",
            message.To, message.Body);
        return Task.CompletedTask;
    }
}
