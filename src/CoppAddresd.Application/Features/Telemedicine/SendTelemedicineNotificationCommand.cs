using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Telemedicine;

/// <summary>
/// Payload de <c>POST /api/v1/internal/telemedicine/notifications</c> (F2).
/// El microservicio de Telemedicina decide QUÉ/CUÁNDO notificar (recordatorio,
/// cancelación, no-show) y este endpoint entrega por los canales disponibles.
/// Contrato fijo acordado con el micro:
/// <c>{ userId, title, body, channels: ["Push","Sms"], data, dedupeKey? }</c>.
/// </summary>
public sealed record SendTelemedicineNotificationRequest(
    Guid UserId,
    string Title,
    string Body,
    IReadOnlyList<string>? Channels,
    Dictionary<string, string>? Data,
    string? DedupeKey = null);

/// <summary>Comando MediatR del endpoint interno (validado por FluentValidation → 400).</summary>
public sealed record SendTelemedicineNotificationCommand(
    Guid UserId,
    string Title,
    string Body,
    IReadOnlyList<string>? Channels,
    Dictionary<string, string>? Data,
    string? DedupeKey = null) : IRequest<SendTelemedicineNotificationResult>;

/// <summary>
/// Resultado por canal (contrato fijo): <c>push</c> y <c>sms</c> con
/// <c>sent | skipped | failed | disabled</c>. Un canal no solicitado o ya
/// deduplicado devuelve <c>skipped</c>.
/// </summary>
public sealed record SendTelemedicineNotificationResult(string Push, string Sms);

/// <summary>Vocabulario de estado por canal (mismo que HealthTests/FcmSendStatus).</summary>
public static class TelemedicineNotificationStatus
{
    public const string Sent = "sent";
    public const string Skipped = "skipped";
    public const string Failed = "failed";
    public const string Disabled = "disabled";
}

/// <summary>
/// Entrega de una notificación interna de Telemedicina (F2) reutilizando la
/// infraestructura existente del API principal:
/// <list type="bullet">
/// <item>Push: <see cref="IDeviceTokenRepository"/> (todos los tokens del
/// usuario, cualquier plataforma — incluida <c>web</c>) + <see cref="IFcmClient"/>.
/// Los tokens obsoletos se eliminan; sin tokens → <c>skipped</c>; FCM sin
/// credenciales → <c>disabled</c>.</item>
/// <item>SMS: teléfono resuelto server-side desde el perfil del paciente
/// (<c>app.patient_profiles</c> por <c>UserId</c>) o del empleado
/// (<c>erp.employees</c>); se envía con <see cref="ISmsSender"/> (Twilio
/// Messages o Noop). Sin teléfono → <c>skipped</c>; canal sin configurar →
/// <c>disabled</c>.</item>
/// <item>Dedupe: con <c>dedupeKey</c>, el estado por canal se persiste en
/// <c>app.notification_dedupe_keys</c> (índice único). Un canal ya <c>sent</c>
/// no se reenvía (idempotencia); los canales en <c>failed | disabled | skipped</c>
/// sí pueden reintentarse con la misma clave sin duplicar lo ya entregado.</item>
/// </list>
/// Un fallo del proveedor (o del SDK) NUNCA propaga: se registra y el canal
/// responde <c>failed</c> (el endpoint jamás devuelve 5xx por proveedor).
/// </summary>
public sealed class SendTelemedicineNotificationCommandHandler(
    IDeviceTokenRepository deviceTokenRepository,
    IFcmClient fcmClient,
    ISmsSender smsSender,
    IPatientRepository patientRepository,
    IEmployeeRepository employeeRepository,
    INotificationDedupeRepository dedupeRepository,
    ILogger<SendTelemedicineNotificationCommandHandler> logger)
    : IRequestHandler<SendTelemedicineNotificationCommand, SendTelemedicineNotificationResult>
{
    public async Task<SendTelemedicineNotificationResult> Handle(
        SendTelemedicineNotificationCommand request, CancellationToken ct)
    {
        var channels = NormalizeChannels(request.Channels);

        // Dedupe por clave (si viene): el estado persistido decide si un canal
        // ya se entregó (no se reenvía) o puede reintentarse.
        var dedupeKey = string.IsNullOrWhiteSpace(request.DedupeKey)
            ? null
            : request.DedupeKey.Trim();

        NotificationDedupeKey? previous = null;
        if (dedupeKey is not null)
        {
            previous = await dedupeRepository.GetByKeyAsync(dedupeKey, ct);
        }

        var pushStatus = channels.Contains("push")
            ? await ResolvePushAsync(previous, request, ct)
            : TelemedicineNotificationStatus.Skipped;

        var smsStatus = channels.Contains("sms")
            ? await ResolveSmsAsync(previous, request, ct)
            : TelemedicineNotificationStatus.Skipped;

        if (dedupeKey is not null)
        {
            await dedupeRepository.UpsertAsync(
                dedupeKey, request.UserId, pushStatus, smsStatus, ct);
        }

        logger.LogInformation(
            "Notificación telemedicina procesada: userId={UserId}, push={Push}, sms={Sms}.",
            request.UserId, pushStatus, smsStatus);

        return new SendTelemedicineNotificationResult(pushStatus, smsStatus);
    }

    /// <summary>
    /// Push idempotente por dedupe: si la clave ya tiene el canal push
    /// <c>sent</c>, se reutiliza el estado sin reenviar; si no, se intenta.
    /// </summary>
    private async Task<string> ResolvePushAsync(
        NotificationDedupeKey? previous,
        SendTelemedicineNotificationCommand request,
        CancellationToken ct)
    {
        if (previous?.PushStatus == TelemedicineNotificationStatus.Sent)
        {
            logger.LogInformation(
                "Push telemedicina ya entregado (dedupeKey={DedupeKey}): no se reenvía.",
                previous.DedupeKey);
            return TelemedicineNotificationStatus.Sent;
        }

        return await SendPushAsync(request, ct);
    }

    /// <summary>
    /// SMS idempotente por dedupe: si la clave ya tiene el canal SMS
    /// <c>sent</c>, se reutiliza el estado sin reenviar; si no, se intenta
    /// (reintento de canales <c>failed | disabled | skipped</c>).
    /// </summary>
    private async Task<string> ResolveSmsAsync(
        NotificationDedupeKey? previous,
        SendTelemedicineNotificationCommand request,
        CancellationToken ct)
    {
        if (previous?.SmsStatus == TelemedicineNotificationStatus.Sent)
        {
            logger.LogInformation(
                "SMS telemedicina ya entregado (dedupeKey={DedupeKey}): no se reenvía.",
                previous.DedupeKey);
            return TelemedicineNotificationStatus.Sent;
        }

        return await SendSmsAsync(request, ct);
    }

    /// <summary>
    /// Fan-out push a todos los tokens del usuario. Sin tokens → <c>skipped</c>;
    /// con al menos un envío aceptado → <c>sent</c>; todos degradados por FCM
    /// deshabilitado → <c>disabled</c>; el resto → <c>failed</c>.
    /// </summary>
    private async Task<string> SendPushAsync(
        SendTelemedicineNotificationCommand request, CancellationToken ct)
    {
        try
        {
            var tokens = await deviceTokenRepository.GetByUserIdAsync(request.UserId, ct);
            if (tokens.Count == 0)
            {
                logger.LogInformation(
                    "Push telemedicina omitido: userId={UserId} sin dispositivos registrados.",
                    request.UserId);
                return TelemedicineNotificationStatus.Skipped;
            }

            var data = request.Data is { Count: > 0 }
                ? new Dictionary<string, string>(request.Data)
                : null;

            var sent = 0;
            var failed = 0;
            var disabled = 0;

            foreach (var token in tokens)
            {
                var result = await fcmClient.SendAsync(
                    token.Token, request.Title, request.Body, data, ct);

                switch (result.Status)
                {
                    case FcmSendStatus.Sent:
                        sent++;
                        break;

                    case FcmSendStatus.Disabled:
                        disabled++;
                        break;

                    case FcmSendStatus.TokenInvalid:
                        failed++;
                        await deviceTokenRepository.DeleteByTokenAsync(token.Token, ct);
                        logger.LogInformation(
                            "Token FCM obsoleto eliminado en notificación telemedicina: tokenId={TokenId}.",
                            token.Id);
                        break;

                    default:
                        failed++;
                        logger.LogWarning(
                            "Push telemedicina falló: tokenId={TokenId}, error={ErrorCode}.",
                            token.Id, result.ErrorCode);
                        break;
                }
            }

            if (sent > 0)
            {
                return TelemedicineNotificationStatus.Sent;
            }

            return disabled == tokens.Count
                ? TelemedicineNotificationStatus.Disabled
                : TelemedicineNotificationStatus.Failed;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error inesperado en push telemedicina (userId={UserId}).", request.UserId);
            return TelemedicineNotificationStatus.Failed;
        }
    }

    /// <summary>
    /// SMS al teléfono resuelto server-side: sin configurar → <c>disabled</c>,
    /// sin teléfono → <c>skipped</c>, resultado del proveedor → <c>sent</c> o
    /// <c>failed</c> (las excepciones se capturan).
    /// </summary>
    private async Task<string> SendSmsAsync(
        SendTelemedicineNotificationCommand request, CancellationToken ct)
    {
        if (!smsSender.IsConfigured)
        {
            logger.LogInformation(
                "SMS telemedicina omitido: proveedor '{Provider}' sin configuración (userId={UserId}).",
                smsSender.Provider, request.UserId);
            return TelemedicineNotificationStatus.Disabled;
        }

        var phone = await ResolvePhoneAsync(request.UserId, ct);
        if (phone is null)
        {
            logger.LogInformation(
                "SMS telemedicina omitido: userId={UserId} sin teléfono registrado.",
                request.UserId);
            return TelemedicineNotificationStatus.Skipped;
        }

        try
        {
            var result = await smsSender.SendAsync(phone, request.Body, ct);
            if (result.Success)
            {
                return TelemedicineNotificationStatus.Sent;
            }

            logger.LogWarning(
                "SMS telemedicina falló (provider={Provider}, to={Phone}, error={Error}).",
                smsSender.Provider, MaskPhone(phone), result.Error);
            return TelemedicineNotificationStatus.Failed;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error inesperado en SMS telemedicina (userId={UserId}).", request.UserId);
            return TelemedicineNotificationStatus.Failed;
        }
    }

    /// <summary>
    /// Resolución server-side del teléfono por <c>userId</c>: primero el perfil
    /// del paciente (<c>app.patient_profiles.UserId</c>) y, si no existe, el
    /// empleado/profesional (<c>erp.employees.UserId</c>). El microservicio
    /// nunca recibe ni envía el teléfono.
    /// </summary>
    private async Task<string?> ResolvePhoneAsync(Guid userId, CancellationToken ct)
    {
        var patient = await patientRepository.GetByUserIdAsync(userId, ct);
        var phone = BuildE164(patient?.PhoneCountryCode, patient?.PhoneNumber);
        if (phone is not null)
        {
            return phone;
        }

        var employee = await employeeRepository.GetByUserIdAsync(userId, ct);
        return BuildE164(employee?.PhoneCountryCode, employee?.PhoneNumber);
    }

    /// <summary>
    /// Normaliza a <c>"push"</c>/<c>"sms"</c> en minúsculas (el contrato acepta
    /// <c>"Push"</c>/<c>"Sms"</c>); los valores desconocidos los rechaza el
    /// validador antes de llegar al handler.
    /// </summary>
    private static HashSet<string> NormalizeChannels(IReadOnlyList<string>? channels) =>
        (channels ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToLowerInvariant())
            .ToHashSet();

    /// <summary>
    /// Construye E.164 (<c>+</c> + dígitos) a partir de
    /// <c>PhoneCountryCode</c> (E.164 sin '+', ej. "57") y <c>PhoneNumber</c>.
    /// Si el número ya viene con '+', se respeta; si ya incluye el código de
    /// país, no se duplica.
    /// </summary>
    internal static string? BuildE164(string? countryCode, string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return null;
        }

        var trimmed = phoneNumber.Trim();
        if (trimmed.StartsWith('+'))
        {
            var plusDigits = KeepDigits(trimmed);
            return plusDigits.Length == 0 ? null : "+" + plusDigits;
        }

        var local = KeepDigits(trimmed);
        if (local.Length == 0)
        {
            return null;
        }

        var cc = KeepDigits(countryCode ?? string.Empty);
        if (cc.Length > 0 && !local.StartsWith(cc, StringComparison.Ordinal))
        {
            local = cc + local;
        }

        return "+" + local;
    }

    private static string KeepDigits(string value) =>
        new(value.Where(char.IsDigit).ToArray());

    /// <summary>Enmascara el teléfono para logs (sin PHI innecesaria).</summary>
    private static string MaskPhone(string phoneNumber)
    {
        var digits = phoneNumber.TrimStart('+');
        return digits.Length <= 4 ? "****" : "+" + new string('*', digits.Length - 4) + digits[^4..];
    }
}
