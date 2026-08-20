using System.Text.Json;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Reglas compartidas del encuentro clínico: serialización del <c>clinical_data</c>
/// (jsonb) y validación de estado. El jsonb se serializa en camelCase e ignorando
/// nulos (contrato estable para lecturas futuras); la lectura es tolerante a
/// campos desconocidos (el ERP puede ampliar el esquema sin romper).
/// </summary>
internal static class EncounterSupport
{
    private static readonly JsonSerializerOptions ClinicalDataOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static string? Serialize(ClinicalDataDto? data)
        => data is null
            ? null
            : JsonSerializer.Serialize(data, ClinicalDataOptions);

    public static ClinicalDataDto? Deserialize(string? json)
        => string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<ClinicalDataDto>(json, ClinicalDataOptions);

    /// <summary>
    /// La documentación clínica solo tiene sentido mientras la consulta está en
    /// curso o ya finalizó (completar la nota después de la cita es válido).
    /// Antes de iniciar (<c>Requested/Confirmed</c>) o con la cita cancelada /
    /// no-show no se permite documentar.
    /// </summary>
    public static void EnsureCanDocument(AppointmentStatus appointmentStatus)
    {
        if (appointmentStatus is not (AppointmentStatus.InProgress or AppointmentStatus.Completed))
        {
            throw new BusinessRuleViolationException(
                $"La cita no admite documentación clínica en su estado actual ({appointmentStatus}).");
        }
    }

    /// <summary>
    /// El registro clínico es inmutable una vez completado: guardar un borrador
    /// sobre un encuentro <c>Completed</c> es una violación (no se pueden
    /// reescribir datos clínicos finalizados sin trazabilidad).
    /// </summary>
    public static void EnsureEditable(EncounterStatus status)
    {
        if (status == EncounterStatus.Completed)
        {
            throw new BusinessRuleViolationException(
                "El registro clínico ya fue completado y es inmutable.");
        }
    }

    /// <summary>
    /// Completar exige contenido mínimo: al menos una nota o un campo clínico
    /// estructurado. Evita registros vacíos sin valor clínico.
    /// </summary>
    public static bool HasContent(ClinicalDataDto? data, string? notes)
        => !string.IsNullOrWhiteSpace(notes)
           || data is { } d && (d.MotivoConsulta is not null
                                || d.Evaluacion is not null
                                || d.Diagnostico is not null
                                || d.Plan is not null
                                || d.Indicaciones is not null
                                || d.Observaciones is not null
                                || d.Seguimiento is not null);
}
