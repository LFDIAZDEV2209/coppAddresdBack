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
/// Proveedor SMS real de <see cref="ISmsSender"/> sobre Twilio Messages
/// (SPEC A13, F2). Sustituye a <see cref="NoOpSmsSender"/> cuando
/// <c>Sms:Provider=Twilio</c> y hay credenciales completas
/// (<c>Sms:IsEnabled/AccountSid/AuthToken</c> + <c>FromNumber</c> o
/// <c>MessagingServiceSid</c>); la selección vive en <c>AddSmsSender</c>.
///
/// Twilio Verify (Auth) NO sirve para SMS libres: este sender usa el recurso
/// Messages. Degradación por diseño: sin configuración no lanza, registra un
/// Warning y devuelve <see cref="SmsSendResult"/> con error; los fallos del
/// proveedor se capturan y se traducen a resultado fallido para que el endpoint
/// interno que lo consume jamás responda 5xx.
/// </summary>
public sealed class TwilioSmsSender(
    IOptions<SmsSettings> settings,
    ITwilioRestClient client,
    ILogger<TwilioSmsSender> logger) : ISmsSender
{
    private readonly SmsSettings _settings = settings.Value;

    /// <inheritdoc />
    public string Provider => "twilio";

    /// <inheritdoc />
    public bool IsConfigured => _settings.IsConfigured;

    /// <inheritdoc />
    public async Task<SmsSendResult> SendAsync(
        string phoneNumber,
        string body,
        CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            logger.LogWarning(
                "Twilio SMS no configurado (Sms:IsEnabled/AccountSid/AuthToken/FromNumber o MessagingServiceSid) — envío omitido."
            );
            return new SmsSendResult(false, null, "Twilio SMS no configurado.");
        }

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return new SmsSendResult(false, null, "El teléfono del destinatario es requerido.");
        }

        ct.ThrowIfCancellationRequested();

        var options = new CreateMessageOptions(new PhoneNumber(phoneNumber.Trim()))
        {
            Body = body,
        };

        if (!string.IsNullOrWhiteSpace(_settings.MessagingServiceSid))
        {
            options.MessagingServiceSid = _settings.MessagingServiceSid;
        }

        if (!string.IsNullOrWhiteSpace(_settings.FromNumber))
        {
            options.From = new PhoneNumber(_settings.FromNumber);
        }

        try
        {
            var message = await MessageResource.CreateAsync(options, client);
            ct.ThrowIfCancellationRequested();

            logger.LogInformation(
                "SMS Twilio aceptado (Sid={Sid}, To={Phone}, Status={Status}).",
                message.Sid, MaskPhone(phoneNumber), message.Status);

            return new SmsSendResult(true, message.Sid, null);
        }
        catch (ApiException ex)
        {
            // Fallo del proveedor (4xx/5xx): se reporta como resultado fallido,
            // nunca se propaga al endpoint interno.
            logger.LogError(
                "Twilio Messages rechazó el SMS (To={Phone}, Status={Status}, Code={Code}).",
                MaskPhone(phoneNumber), ex.Status, ex.Code);
            return new SmsSendResult(false, null, $"twilio:{ex.Code}");
        }
        catch (TwilioException ex)
        {
            // Error de transporte/conexión con Twilio.
            logger.LogError(ex, "Error de transporte al enviar SMS Twilio a {Phone}.", MaskPhone(phoneNumber));
            return new SmsSendResult(false, null, "twilio:transport");
        }
    }

    /// <summary>
    /// Enmascara el teléfono para logging: conserva el '+' y los últimos 4
    /// dígitos (ej. <c>+********5500</c>). Nunca se registra el número completo
    /// (sin PHI innecesaria).
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
