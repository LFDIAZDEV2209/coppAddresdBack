using System.Security;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Twilio.Clients;
using Twilio.Exceptions;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Proveedor de voz real de <see cref="IVoiceCaller"/> sobre Twilio Calls
/// (canal de emergencia SOS): llama al contacto y reproduce el guion TTS con
/// TwiML <c>&lt;Say&gt;</c> (Polly.Lupe para español, Polly.Joanna para inglés).
/// Sustituye a <see cref="NoOpVoiceCaller"/> cuando <c>Voice:Provider=Twilio</c>
/// y hay credenciales completas; la selección vive en <c>AddVoiceCaller</c>.
///
/// Autenticación con el Auth Token de la cuenta (mismo patrón que Twilio
/// Messages). Contrato fail-soft: sin configuración no lanza (Warning +
/// resultado fallido) y los errores del proveedor se traducen a
/// <see cref="VoiceCallResult"/> para que el despachador jamás propague un 5xx.
/// Los logs nunca incluyen el guion completo (sin PHI innecesaria).
/// </summary>
public sealed class TwilioVoiceCaller : IVoiceCaller
{
    private readonly VoiceSettings _settings;
    private readonly ILogger<TwilioVoiceCaller> _logger;
    private readonly Lazy<ITwilioRestClient> _client;

    public TwilioVoiceCaller(
        IOptions<VoiceSettings> settings,
        ILogger<TwilioVoiceCaller> logger
    )
    {
        _settings = settings.Value;
        _logger = logger;

        // Cliente propio: la voz no depende del ITwilioRestClient de SMS (puede
        // estar configurada con credenciales distintas) y es thread-safe.
        _client = new Lazy<ITwilioRestClient>(() =>
            new TwilioRestClient(_settings.AccountSid, _settings.AuthToken)
        );
    }

    /// <inheritdoc />
    public string Provider => "twilio";

    /// <inheritdoc />
    public bool IsConfigured => _settings.IsConfigured;

    /// <inheritdoc />
    public async Task<VoiceCallResult> CallAsync(
        string phoneNumber,
        string sayText,
        string language,
        CancellationToken ct = default,
        string? statusCallbackUrl = null
    )
    {
        if (!IsConfigured)
        {
            _logger.LogWarning(
                "Twilio Voice no configurado (Voice:IsEnabled/AccountSid/AuthToken/FromNumber) — llamada omitida."
            );
            return new VoiceCallResult(false, null, "Twilio Voice no configurado.");
        }

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return new VoiceCallResult(false, null, "El teléfono del destinatario es requerido.");
        }

        ct.ThrowIfCancellationRequested();

        var normalizedLanguage = string.IsNullOrWhiteSpace(language) ? "es-US" : language.Trim();
        var voice = normalizedLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase)
            ? "Polly.Joanna"
            : "Polly.Lupe";

        var options = new CreateCallOptions(
            new PhoneNumber(phoneNumber.Trim()),
            new PhoneNumber(_settings.FromNumber)
        )
        {
            Twiml = BuildTwiml(sayText, normalizedLanguage, voice),
        };

        if (!string.IsNullOrWhiteSpace(statusCallbackUrl))
        {
            options.StatusCallback = new Uri(statusCallbackUrl);
            options.StatusCallbackEvent = ["initiated", "ringing", "answered", "completed"];

            // AMD asíncrono: la llamada suena y reproduce el mensaje de inmediato;
            // Twilio reporta human/machine_start/unknown en un callback aparte
            // (mismo endpoint de voz) para no retrasar la alerta.
            options.AsyncAmd = "true";
            options.AsyncAmdStatusCallback = new Uri(statusCallbackUrl);
            options.AsyncAmdStatusCallbackMethod = Twilio.Http.HttpMethod.Post;
        }

        try
        {
            var call = await CallResource.CreateAsync(options, _client.Value);
            ct.ThrowIfCancellationRequested();

            _logger.LogInformation(
                "Llamada Twilio aceptada (Sid={Sid}, To={Phone}, Status={Status}).",
                call.Sid,
                MaskPhone(phoneNumber),
                call.Status
            );

            return new VoiceCallResult(true, call.Sid, null);
        }
        catch (ApiException ex)
        {
            // Fallo del proveedor (4xx/5xx): se reporta como resultado fallido,
            // nunca se propaga al despachador.
            _logger.LogError(
                "Twilio Calls rechazó la llamada (To={Phone}, Status={Status}, Code={Code}).",
                MaskPhone(phoneNumber),
                ex.Status,
                ex.Code
            );
            return new VoiceCallResult(false, null, $"twilio:{ex.Code}");
        }
        catch (TwilioException ex)
        {
            // Error de transporte/conexión con Twilio.
            _logger.LogError(ex, "Error de transporte al llamar con Twilio a {Phone}.", MaskPhone(phoneNumber));
            return new VoiceCallResult(false, null, "twilio:transport");
        }
    }

    /// <summary>
    /// Construye el TwiML del guion TTS escapando el texto (previene inyección
    /// XML en <c>&lt;Say&gt;</c>).
    /// </summary>
    private static string BuildTwiml(string sayText, string language, string voice)
    {
        var escaped = SecurityElement.Escape(sayText) ?? string.Empty;
        return $"<Response><Say language=\"{language}\" voice=\"{voice}\">{escaped}</Say></Response>";
    }

    /// <summary>
    /// Enmascara el teléfono para logging: conserva el '+' y los últimos 4
    /// dígitos (misma convención que TwilioSmsSender).
    /// </summary>
    private static string MaskPhone(string phoneNumber)
    {
        var digits = phoneNumber.TrimStart('+');
        if (digits.Length <= 4)
        {
            return "****";
        }

        return "+" + new string('*', digits.Length - 4) + digits[^4..];
    }
}
