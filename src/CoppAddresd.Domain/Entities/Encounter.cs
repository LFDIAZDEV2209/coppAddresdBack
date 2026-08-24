namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Encounter canónico (contenedor de la visita clínica) que usan las mediciones.
/// Es el registro clínico general: liviano y estructurado (<see cref="Reason"/> +
/// <see cref="Notes"/>); el detalle rico de la sesión (appointment, clinical_data)
/// permanece en el módulo tele, que se linkea con una FK nullable
/// (<c>tele.clinical_encounters.encounter_id</c>). ADR-001 / ADR-004.
/// </summary>
public sealed class Encounter
{
    public Guid Id { get; set; }

    /// <summary>Paciente de la visita.</summary>
    public Guid PatientId { get; set; }

    /// <summary>Profesional que atendió la visita.</summary>
    public Guid ProfessionalId { get; set; }

    /// <summary>Tipo de encuentro: <c>consulta_periodica</c> | <c>telemedicina</c> | <c>seguimiento</c>.</summary>
    public string Type { get; set; } = default!;

    /// <summary>Estado: <c>planned</c> | <c>in_progress</c> | <c>completed</c> | <c>cancelled</c>.</summary>
    public string Status { get; set; } = "planned";

    /// <summary>Inicio de la visita. Null mientras esté planificado.</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>Fin de la visita. Null hasta que se complete.</summary>
    public DateTime? EndedAt { get; set; }

    /// <summary>Motivo de consulta.</summary>
    public string? Reason { get; set; }

    public string? Notes { get; set; }

    /// <summary>Usuario de <c>auth.users</c> que creó el registro (auditoría).</summary>
    public Guid? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Último usuario de <c>auth.users</c> que modificó el registro (auditoría).</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Paciente de la visita.</summary>
    public PatientProfile? Patient { get; set; }

    /// <summary>Profesional que atendió la visita (schema <c>erp</c>).</summary>
    public Professional? Professional { get; set; }

    /// <summary>Mediciones registradas durante el encuentro.</summary>
    public ICollection<ClinicalMeasurement> Measurements { get; set; } = [];
}