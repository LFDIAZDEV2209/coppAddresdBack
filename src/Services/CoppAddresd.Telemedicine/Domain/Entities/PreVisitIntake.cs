namespace CoppAddresd.Telemedicine.Domain.Entities;

/// <summary>
/// Pre-consulta del paciente (F4): datos subjetivos que el paciente reporta
/// antes de la consulta (motivo, síntomas, alergias y medicación opcional).
/// Es 1:1 con la cita (índice único en <c>appointment_id</c>) y editable
/// mientras la cita siga <c>Confirmed</c>; a partir de <c>InProgress</c> queda
/// en solo lectura. El autor (<see cref="CreatedBy"/>) y el paciente
/// (<see cref="PatientId"/>) se derivan del JWT y de la cita, nunca del cuerpo.
/// </summary>
public sealed class PreVisitIntake
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Cita a la que pertenece la pre-consulta (1:1).</summary>
    public Guid AppointmentId { get; set; }

    /// <summary>Paciente de la cita (referencia débil a <c>app.patient_profiles</c>).</summary>
    public Guid PatientId { get; set; }

    /// <summary>Motivo de consulta reportado por el paciente (obligatorio, máx. 500).</summary>
    public string Reason { get; set; } = default!;

    /// <summary>Síntomas reportados (texto libre, máx. 4000).</summary>
    public string? Symptoms { get; set; }

    /// <summary>Alergias declaradas por el paciente (texto libre, máx. 2000).</summary>
    public string? Allergies { get; set; }

    /// <summary>Medicación actual (opcional, texto libre, máx. 2000).</summary>
    public string? Medications { get; set; }

    /// <summary>Usuario de <c>auth.users</c> que guardó la pre-consulta (JWT).</summary>
    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Appointment? Appointment { get; set; }
}
