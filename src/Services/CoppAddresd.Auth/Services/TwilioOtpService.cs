using System.Text.RegularExpressions;
using CoppAddresd.Auth.Configuration;
using CoppAddresd.Auth.Exceptions;
using CoppAddresd.Auth.Interfaces;
using Microsoft.Extensions.Options;
using Twilio.Clients;
using Twilio.Exceptions;
using Twilio.Rest.Verify.V2.Service;

namespace CoppAddresd.Auth.Services;

/// <summary>
/// Cliente de Twilio Verify V2 para OTP por SMS. Envuelve el SDK oficial de
/// Twilio tras <see cref="ITwilioOtpService"/> (tipos propios del proyecto) y
/// usa autenticación por API Key (ApiKeySid + ApiKeySecret), nunca el Auth
/// Token maestro. Twilio es quien genera y valida el código: este servicio NO
/// genera, persiste ni hashea OTP (no toca <c>auth.otp_codes</c>).
/// </summary>
public class TwilioOtpService : ITwilioOtpService
{
    // Canal SMS de Twilio Verify V2.
    private const string ChannelSms = "sms";

    // E.164: '+' + código de país (1-9, sin ceros a la izquierda) + hasta 14
    // dígitos más (máximo 15 dígitos en total, estándar E.164). No se adivina
    // país ni se agrega prefijo: el llamador entrega el número completo.
    private static readonly Regex PhoneE164Regex = new(
        @"^\+[1-9]\d{6,14}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private const string StatusApproved = "approved";

    private readonly TwilioSettings _settings;
    private readonly ITwilioRestClient _client;
    private readonly ILogger<TwilioOtpService> _logger;

    public TwilioOtpService(
        IOptions<TwilioSettings> settings,
        ITwilioRestClient client,
        ILogger<TwilioOtpService> logger)
    {
        _settings = settings.Value;
        _client = client;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TwilioSendOtpResult> SendAsync(string phoneNumber, CancellationToken ct = default)
    {
        EnsureEnabled();
        EnsureValidPhone(phoneNumber);

        // Nota de limitación: el SDK de Twilio no expone CancellationToken en
        // los métodos de recurso. Se hace un chequeo cooperativo antes y
        // después de la llamada; la petición HTTP en vuelo no es cancelable.
        ct.ThrowIfCancellationRequested();

        var options = new CreateVerificationOptions(
            pathServiceSid: _settings.VerifyServiceSid,
            to: phoneNumber,
            channel: ChannelSms);

        try
        {
            _logger.LogInformation(
                "Twilio OTP verification requested for phone {Phone}",
                MaskPhone(phoneNumber));

            var verification = await VerificationResource.CreateAsync(options, _client);

            ct.ThrowIfCancellationRequested();

            _logger.LogInformation(
                "Twilio OTP verification created (Status={Status})",
                verification.Status);

            return new TwilioSendOtpResult(verification.Sid, verification.Status);
        }
        catch (ApiException ex) when (!ct.IsCancellationRequested)
        {
            throw MapApiException(ex);
        }
        catch (TwilioException ex) when (!ct.IsCancellationRequested)
        {
            throw MapTransportException(ex);
        }
    }

    /// <inheritdoc />
    public async Task<TwilioCheckOtpResult> CheckAsync(string phoneNumber, string code, CancellationToken ct = default)
    {
        EnsureEnabled();
        EnsureValidPhone(phoneNumber);

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new TwilioOtpException(
                TwilioOtpErrorKind.InvalidParameter,
                "El código OTP es requerido.");
        }

        ct.ThrowIfCancellationRequested();

        var options = new CreateVerificationCheckOptions(_settings.VerifyServiceSid)
        {
            To = phoneNumber,
            Code = code,
        };

        try
        {
            _logger.LogInformation(
                "Twilio OTP verification checked for phone {Phone}",
                MaskPhone(phoneNumber));

            var check = await VerificationCheckResource.CreateAsync(options, _client);

            ct.ThrowIfCancellationRequested();

            // Un código incorrecto es un resultado de negocio esperado:
            // Twilio responde con status "pending" (o similar), nunca se lanza
            // excepción. Solo "approved" cuenta como verificación exitosa.
            var isApproved = string.Equals(
                check.Status, StatusApproved, StringComparison.OrdinalIgnoreCase);

            _logger.LogInformation(
                "Twilio OTP verification result (Status={Status}, Approved={Approved})",
                check.Status, isApproved);

            return new TwilioCheckOtpResult(isApproved, check.Status);
        }
        catch (ApiException ex) when (!ct.IsCancellationRequested)
        {
            throw MapApiException(ex);
        }
        catch (TwilioException ex) when (!ct.IsCancellationRequested)
        {
            throw MapTransportException(ex);
        }
    }

    private void EnsureEnabled()
    {
        if (!_settings.IsEnabled)
        {
            throw new TwilioOtpException(
                TwilioOtpErrorKind.Disabled,
                "La integración con Twilio OTP no está habilitada.");
        }
    }

    private static void EnsureValidPhone(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new TwilioOtpException(
                TwilioOtpErrorKind.InvalidPhone,
                "El teléfono es requerido.");
        }

        if (!PhoneE164Regex.IsMatch(phoneNumber))
        {
            throw new TwilioOtpException(
                TwilioOtpErrorKind.InvalidPhone,
                "El teléfono debe estar en formato E.164 (ej. +15765550100).");
        }
    }

    /// <summary>
    /// Mapea un <see cref="ApiException"/> de Twilio a <see cref="TwilioOtpException"/>
    /// conservando solo información útil para el mapeo HTTP (kind, status y
    /// código de error de Twilio). El mensaje se limita a un detalle seguro.
    /// </summary>
    private TwilioOtpException MapApiException(ApiException ex)
    {
        var kind = ex.Status >= 500
            ? TwilioOtpErrorKind.ProviderUnavailable
            : ex.Status == 429
                ? TwilioOtpErrorKind.RateLimited
                : ex.Code is 60200 or 60202 or 60203
                    ? TwilioOtpErrorKind.InvalidPhone
                    : TwilioOtpErrorKind.ProviderError;

        _logger.LogError(
            "Twilio OTP API error (Kind={Kind}, TwilioCode={TwilioCode}, Status={Status})",
            kind, ex.Code, ex.Status);

        return new TwilioOtpException(
            kind,
            "Twilio no pudo completar la operación OTP.",
            ex.Status,
            ex.Code,
            ex);
    }

    /// <summary>
    /// Mapea errores de transporte/conexión (ApiConnectionException y otras
    /// <see cref="TwilioException"/> sin status API) a "proveedor no disponible".
    /// </summary>
    private TwilioOtpException MapTransportException(TwilioException ex)
    {
        _logger.LogError(ex, "Twilio OTP transport error");

        return new TwilioOtpException(
            TwilioOtpErrorKind.ProviderUnavailable,
            "No se pudo contactar a Twilio. Intenta más tarde.",
            innerException: ex);
    }

    /// <summary>
    /// Enmascara el teléfono para logging: conserva el '+' y los últimos 4
    /// dígitos (ej. <c>+********5500</c>). Nunca se registra el número completo.
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
