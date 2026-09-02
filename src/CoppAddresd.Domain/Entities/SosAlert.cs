namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Registro de una alerta SOS activada desde la app móvil del paciente.
/// Persiste el bloque de datos de emergencia generado (para auditoría),
/// los resultados por canal (SMS/email/voz) y el estado consolidado.
/// El paciente se resuelve SIEMPRE del JWT (anti-IDOR); nunca del body.
/// </summary>
public sealed class SosAlert
{
    public Guid Id { get; set; }

    /// <summary>Id del perfil de paciente en <c>app.patient_profiles</c>.</summary>
    public Guid PatientId { get; set; }

    /// <summary>Momento UTC en que se activó la alerta.</summary>
    public DateTime TriggeredAtUtc { get; set; }

    /// <summary>Latitud GPS (null si no disponible).</summary>
    public double? Latitude { get; set; }

    /// <summary>Longitud GPS (null si no disponible).</summary>
    public double? Longitude { get; set; }

    /// <summary>Precisión del GPS en metros.</summary>
    public double? AccuracyMeters { get; set; }

    /// <summary>Etiqueta legible de la ubicación (ej. "Calle 100 #15-20, Bogotá").</summary>
    public string? LocationLabel { get; set; }

    /// <summary>Signos vitales en formato JSON (jsonb en PostgreSQL).</summary>
    public string? VitalsSnapshot { get; set; }

    /// <summary>Bloque de datos de emergencia generado (texto plano, para auditoría). Nunca nulo.</summary>
    public string MessageText { get; set; } = default!;

    /// <summary>Nombre del contacto de emergencia proporcionado por el paciente.</summary>
    public string? EmergencyContactName { get; set; }

    /// <summary>Relación del contacto de emergencia (ej. "Madre", "Esposo").</summary>
    public string? EmergencyContactRelationship { get; set; }

    /// <summary>Teléfono del contacto de emergencia (E.164).</summary>
    public string? EmergencyContactPhone { get; set; }

    /// <summary>Email del contacto de emergencia.</summary>
    public string? EmergencyContactEmail { get; set; }

    /// <summary>Estado consolidado: Sent, Partial, Failed, Disabled.</summary>
    public string Status { get; set; } = default!;

    /// <summary>Resultados por canal en formato JSON (jsonb en PostgreSQL).</summary>
    public string ChannelResults { get; set; } = default!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
