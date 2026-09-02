using System.Security;
using System.Xml;
using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace CoppAddresd.Infrastructure.Services.Sos;

/// <summary>
/// Llamada de voz TTS vía Twilio. Usa TwiML con &lt;Say&gt; para sintetizar
/// el mensaje de emergencia. Inicializa el SDK con el mismo orden que SMS:
/// <c>TwilioClient.Init(ApiKeySid, ApiKeySecret, AccountSid)</c>. NUNCA usa SetRegion.
/// </summary>
public sealed class TwilioVoiceCaller(
    IOptions<TwilioSosOptions> options,
    ILogger<TwilioVoiceCaller> logger) : IVoiceCaller
{
    public async Task CallAsync(string to, string sayText, string language, CancellationToken ct = default)
    {
        var opts = options.Value;

        if (!opts.IsEnabled || !opts.IsConfigured)
        {
            logger.LogWarning("TwilioVoiceCaller: deshabilitado o sin configurar. Llamada a {To} omitida.", to);
            throw new InvalidOperationException("Twilio Voice no configurado o deshabilitado.");
        }

        // Mapeo de código de idioma a voice de Polly
        var voice = language.StartsWith("es", StringComparison.OrdinalIgnoreCase) ? "Polly.Lupe" : "Polly.Joanna";

        // Escapa el texto para XML (prevenir inyección en TwiML)
        var escapedText = SecurityElement.Escape(sayText);

        var twiml = $"""
            <Response>
                <Say language="{language}" voice="{voice}">{escapedText}</Say>
            </Response>
            """;

        // Inicialización con las credenciales de la API Key (orden específico).
        TwilioClient.Init(opts.ApiKeySid, opts.ApiKeySecret, opts.AccountSid);

        try
        {
            var call = await CallResource.CreateAsync(
                to: new Twilio.Types.PhoneNumber(to),
                from: new Twilio.Types.PhoneNumber(opts.FromNumber),
                twiml: twiml);

            logger.LogInformation(
                "Llamada de voz enviada vía Twilio: to={To}, sid={Sid}, status={Status}",
                to, call.Sid, call.Status);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al enviar llamada de voz vía Twilio a {To}.", to);
            throw;
        }
    }
}

/// <summary>
/// Fake de llamada de voz que solo loguea (desarrollo, sin credenciales de Twilio).
/// </summary>
public sealed class LogVoiceCaller(ILogger<LogVoiceCaller> logger) : IVoiceCaller
{
    public Task CallAsync(string to, string sayText, string language, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[SOS-VOICE-LOG] To: {To}, Language: {Language}\nTTS: {Text}",
            to, language, sayText);
        return Task.CompletedTask;
    }
}
