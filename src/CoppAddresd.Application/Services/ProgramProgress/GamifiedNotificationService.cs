using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.ProgramProgress;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Services.ProgramProgress;

/// <summary>
/// Implementación de <see cref="IGamifiedNotificationService"/> (SPEC §20):
/// log en <c>app.notifications</c> vía <see cref="INotificationLogRepository"/>
/// + push FCM reutilizando el camino del módulo de notificaciones
/// (<see cref="IDeviceTokenRepository"/> + <see cref="IFcmClient"/>) — sin
/// duplicar la lógica de envío existente.
///
/// Límites anti-spam configurables (<c>Program:Notifications</c>, defaults):
/// - <c>MaxPerTypePerDay</c> = 2 (máx. por tipo por día local)
/// - <c>MaxPerDay</c> = 6 (máx. total por día local)
/// - <c>QuietHoursStart</c> = 22 / <c>QuietHoursEnd</c> = 7 (horario de
///   silencio en hora local del paciente; la prioridad <c>critical</c> lo ignora)
///
/// Todo el cuerpo es best-effort: las excepciones se registran y no se
/// propagan (AC-42: un fallo del servicio nunca rompe la transacción de XP).
/// </summary>
public sealed class GamifiedNotificationService(
    INotificationLogRepository repository,
    IDeviceTokenRepository deviceTokens,
    IFcmClient fcmClient,
    IConfiguration configuration,
    ILogger<GamifiedNotificationService> logger) : IGamifiedNotificationService
{
    private const string PriorityCritical = "critical";
    private const int DefaultMaxPerTypePerDay = 2;
    private const int DefaultMaxPerDay = 6;
    private const int DefaultQuietHoursStart = 22;
    private const int DefaultQuietHoursEnd = 7;

    private readonly int _maxPerTypePerDay =
        int.TryParse(configuration["Program:Notifications:MaxPerTypePerDay"], out var configuredType)
        && configuredType > 0
            ? configuredType
            : DefaultMaxPerTypePerDay;

    private readonly int _maxPerDay =
        int.TryParse(configuration["Program:Notifications:MaxPerDay"], out var configuredDay)
        && configuredDay > 0
            ? configuredDay
            : DefaultMaxPerDay;

    private readonly int _quietHoursStart =
        int.TryParse(configuration["Program:Notifications:QuietHoursStart"], out var configuredStart)
        && configuredStart is >= 0 and <= 23
            ? configuredStart
            : DefaultQuietHoursStart;

    private readonly int _quietHoursEnd =
        int.TryParse(configuration["Program:Notifications:QuietHoursEnd"], out var configuredEnd)
        && configuredEnd is >= 0 and <= 23
            ? configuredEnd
            : DefaultQuietHoursEnd;

    public async Task NotifyAsync(
        Guid patientId,
        string type,
        string title,
        string message,
        string priority,
        CancellationToken ct = default)
    {
        try
        {
            // 1. Contexto del paciente: timezone (día local + horario de
            //    silencio) y userId (resolución de tokens FCM).
            var context = await repository.GetContextAsync(patientId, ct);
            if (context is null)
            {
                logger.LogDebug(
                    "Notificación gamificada omitida: patient={PatientId} no tiene perfil.", patientId);
                return;
            }

            var timezone = string.IsNullOrWhiteSpace(context.Timezone) ? "UTC" : context.Timezone;
            var nowLocal = NowInPatientZone(timezone);

            // 2. Horario de silencio (SPEC §20, B): 22:00–07:00 local; la
            //    prioridad critical lo ignora.
            if (!string.Equals(priority, PriorityCritical, StringComparison.OrdinalIgnoreCase)
                && IsQuietHour(nowLocal))
            {
                logger.LogDebug(
                    "Notificación {Type} omitida (horario de silencio {Start}:00-{End}:00 local) " +
                    "para patient={PatientId}.",
                    type, _quietHoursStart, _quietHoursEnd, patientId);
                return;
            }

            // 3. Anti-spam (SPEC §20, B): límites por día LOCAL del paciente.
            //    Límite alcanzado → se omite en silencio (log debug).
            var (dayStartUtc, dayEndUtc) = LocalDayRangeUtc(nowLocal, timezone);

            var byTypeToday = await repository.CountByTypeOnDayAsync(
                patientId, type, dayStartUtc, dayEndUtc, ct);
            if (byTypeToday >= _maxPerTypePerDay)
            {
                logger.LogDebug(
                    "Notificación {Type} omitida (anti-spam: {Count}/{Max} por tipo y día) " +
                    "para patient={PatientId}.",
                    type, byTypeToday, _maxPerTypePerDay, patientId);
                return;
            }

            var totalToday = await repository.CountOnDayAsync(
                patientId, dayStartUtc, dayEndUtc, ct);
            if (totalToday >= _maxPerDay)
            {
                logger.LogDebug(
                    "Notificación {Type} omitida (anti-spam: {Count}/{Max} totales por día) " +
                    "para patient={PatientId}.",
                    type, totalToday, _maxPerDay, patientId);
                return;
            }

            // 4. Log (fuente de verdad del centro de notificaciones, SPEC §20, A).
            var notification = new AppNotification
            {
                Id = Guid.NewGuid(),
                PatientId = patientId,
                Type = type,
                Title = title,
                Message = message,
                Priority = priority,
                Channel = "push",
                SentAt = DateTime.UtcNow,
            };
            await repository.AddAsync(notification, ct);

            // 5. Push FCM best-effort (AC-42): un fallo de envío nunca rompe la
            //    transacción de XP; el log ya quedó persistido.
            if (context.UserId is { } userId)
            {
                await SendPushAsync(userId, notification, ct);
            }
        }
        catch (Exception ex)
        {
            // Best-effort (SPEC §20, B): el servicio NUNCA propaga; el flujo de
            // otorgamiento de XP continúa intacto (AC-42).
            logger.LogError(
                ex,
                "Notificación gamificada falló (best-effort): type={Type} patient={PatientId}.",
                type, patientId);
        }
    }

    /// <summary>
    /// Envía el push a todos los dispositivos registrados del usuario por el
    /// camino existente del módulo de notificaciones (fan-out de tokens FCM).
    /// Cada envío es independiente: un token fallido no corta el resto; un
    /// token obsoleto (UNREGISTERED) se elimina para no reintentarlo.
    /// </summary>
    private async Task SendPushAsync(Guid userId, AppNotification notification, CancellationToken ct)
    {
        var tokens = await deviceTokens.GetByUserIdAsync(userId, ct);
        if (tokens.Count == 0)
        {
            logger.LogDebug(
                "Notificación {Type}: sin dispositivos registrados para userId={UserId}.",
                notification.Type, userId);
            return;
        }

        var data = new Dictionary<string, string>
        {
            ["type"] = notification.Type,
            ["patient_id"] = notification.PatientId.ToString(),
        };

        foreach (var token in tokens)
        {
            try
            {
                var result = await fcmClient.SendAsync(
                    token.Token, notification.Title, notification.Message, data, ct);

                switch (result.Status)
                {
                    case FcmSendStatus.Sent:
                        break;

                    case FcmSendStatus.TokenInvalid:
                        // Token obsoleto: se elimina para no reintentarlo en el
                        // próximo envío (mismo manejo que SendPushNotificationCommandHandler).
                        var removed = await deviceTokens.DeleteByTokenAsync(token.Token, ct);
                        logger.LogInformation(
                            "Token FCM obsoleto eliminado (removed={Removed}): tokenId={TokenId}.",
                            removed, token.Id);
                        break;

                    case FcmSendStatus.Disabled:
                        logger.LogDebug(
                            "Push degradado (FCM deshabilitado): tokenId={TokenId}.", token.Id);
                        break;

                    default:
                        logger.LogWarning(
                            "Push FCM falló: tokenId={TokenId}, error={ErrorCode}.",
                            token.Id, result.ErrorCode);
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Push FCM lanzó excepción (best-effort): tokenId={TokenId}.", token.Id);
            }
        }
    }

    /// <summary>
    /// Ahora en la zona del paciente (fallback a UTC si la zona no está
    /// disponible en el SO — Windows mapea las IANA comunes).
    /// </summary>
    private static DateTime NowInPatientZone(string timezone)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateTime.UtcNow;
        }
        catch (InvalidTimeZoneException)
        {
            return DateTime.UtcNow;
        }
    }

    /// <summary>
    /// ¿Está el instante local dentro del horario de silencio configurado?
    /// Rango envolvente (22:00–07:00): hora &gt;= inicio O hora &lt; fin.
    /// </summary>
    private bool IsQuietHour(DateTime localNow)
    {
        if (_quietHoursStart == _quietHoursEnd)
        {
            return false; // ventana vacía: sin horario de silencio
        }

        if (_quietHoursStart < _quietHoursEnd)
        {
            return localNow.Hour >= _quietHoursStart && localNow.Hour < _quietHoursEnd;
        }

        // Rango que cruza la medianoche (default 22:00–07:00).
        return localNow.Hour >= _quietHoursStart || localNow.Hour < _quietHoursEnd;
    }

    /// <summary>
    /// Rango UTC del día local del paciente [medianoche local, medianoche+1d),
    /// respetando DST de la zona IANA (mismo enfoque que el repositorio en
    /// <c>LocalDateToUtcStart</c>). Fallback a tratar la medianoche local como
    /// UTC si la conversión falla (zona no disponible).
    /// </summary>
    private static (DateTime StartUtc, DateTime EndUtc) LocalDayRangeUtc(
        DateTime localNow, string timezone)
    {
        var localDay = localNow.Date;
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(localDay, tz);
            var endUtc = TimeZoneInfo.ConvertTimeToUtc(localDay.AddDays(1), tz);
            return (startUtc, endUtc);
        }
        catch (Exception)
        {
            // Zona no resoluble: el día local se trata como UTC (conservador:
            // el anti-spam cuenta la ventana del día del servidor).
            return (localDay, localDay.AddDays(1));
        }
    }
}