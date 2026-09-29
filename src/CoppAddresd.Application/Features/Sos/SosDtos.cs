using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;

namespace CoppAddresd.Application.Features.Sos;

/// <summary>
/// Vista de la alerta SOS para el dueño (app móvil) y para el staff ERP con
/// scope. El teléfono de destino SIEMPRE se devuelve enmascarado (conserva
/// '+' y los últimos 4 dígitos): el paciente ya conoce su contacto
/// (<c>/me/patient-profile</c>) y el staff no necesita el número completo.
/// </summary>
public record SosAlertDto(
    Guid Id,
    Guid PatientId,
    string Status,
    double? Latitude,
    double? Longitude,
    double? AccuracyMeters,
    DateTime? LocationCapturedAt,
    string MaskedDestinationPhone,
    string SmsChannelStatus,
    string PushChannelStatus,
    int? PushRecipients,
    DateTime CreatedAt,
    Guid? AttendedBy,
    DateTime? AttendedAt,
    Guid? CancelledBy,
    DateTime? CancelledAt
)
{
    public static SosAlertDto FromEntity(SosAlert alert)
    {
        var maskedPhone = MaskPhone(alert.DestinationPhoneE164);
        return new SosAlertDto(
            alert.Id,
            alert.PatientId,
            alert.Status.ToString(),
            alert.Latitude,
            alert.Longitude,
            alert.AccuracyMeters,
            alert.LocationCapturedAt,
            maskedPhone,
            alert.SmsChannelStatus.ToString(),
            alert.PushChannelStatus.ToString(),
            alert.PushRecipients,
            alert.CreatedAt,
            alert.AttendedBy,
            alert.AttendedAt,
            alert.CancelledBy,
            alert.CancelledAt
        );
    }

    /// <summary>
    /// Enmascara el teléfono para respuestas y logs: conserva el '+' y los
    /// últimos 4 dígitos (misma convención que TwilioSmsSender). Nunca se
    /// registra ni devuelve el número completo.
    /// </summary>
    public static string MaskPhone(string phoneNumber)
    {
        var digits = phoneNumber.TrimStart('+');
        if (digits.Length <= 4)
        {
            return "****";
        }

        return "+" + new string('*', digits.Length - 4) + digits[^4..];
    }
}

/// <summary>Cuerpo de <c>POST /api/v1/sos/alerts</c>. El número de destino y el texto del SMS NUNCA viajan en el body (D3).</summary>
public record ActivateSosAlertRequest(
    double? Latitude = null,
    double? Longitude = null,
    double? AccuracyMeters = null,
    DateTime? LocationCapturedAt = null
);

/// <summary>Resultado de la activación para el controller (mapeo exacto a HTTP).</summary>
public enum SosActivationOutcome
{
    /// <summary>Alerta creada → 201.</summary>
    Created,

    /// <summary>Misma Idempotency-Key con payload idéntico → 200 con la alerta original.</summary>
    Replayed,

    /// <summary>Misma Idempotency-Key con payload distinto → 409.</summary>
    IdempotencyConflict,

    /// <summary>El paciente ya tiene otra alerta activa (clave distinta) → 409 con la referencia.</summary>
    ActiveExists,

    /// <summary>Cuota/cooldown excedido (Valkey) → 429 con Retry-After.</summary>
    RateLimited,
}

/// <summary>Salida del handler de activación: alerta + outcome + Retry-After para 429.</summary>
public sealed record ActivateSosAlertResult(
    SosActivationOutcome Outcome,
    SosAlertDto? Alert,
    int RetryAfterSeconds
);

/// <summary>Resultado de la transición terminal (attend/cancel) para el controller.</summary>
public enum SosTransitionOutcome
{
    /// <summary>Transición aplicada → 200.</summary>
    Transited,

    /// <summary>Alerta inexistente o ajena (no revelador) → 404.</summary>
    NotFound,

    /// <summary>La alerta no está Activa (estado terminal) → 409.</summary>
    NotActive,

    /// <summary>Staff sin scope clínico sobre el paciente (no revelador) → 403.</summary>
    Forbidden,
}

public sealed record SosTransitionResult(SosTransitionOutcome Outcome, SosAlertDto? Alert);

// --- Listado staff (ERP) ---

/// <summary>
/// Fila del listado de alertas para el staff ERP (bandeja SOS). Sin PII
/// innecesaria: NO incluye teléfono destino ni coordenadas GPS (eso vive en
/// el detalle <c>GET /{{id}}</c>, sujeto al mismo scope); el nombre del
/// paciente es el dato operacional mínimo para responder la alerta.
/// </summary>
public record SosAlertListItemDto(
    Guid Id,
    Guid PatientId,
    string? PatientName,
    string Status,
    DateTime CreatedAt,
    Guid? AttendedBy,
    DateTime? AttendedAt,
    Guid? CancelledBy,
    DateTime? CancelledAt,
    string SmsChannelStatus,
    string PushChannelStatus
);

/// <summary>Página de listado staff (convención de paginación offset del repo).</summary>
public record SosAlertsPage(
    IReadOnlyList<SosAlertListItemDto> Data,
    int Total,
    int Page,
    int PageSize,
    int TotalPages
);
