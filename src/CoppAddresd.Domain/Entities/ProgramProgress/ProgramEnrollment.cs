using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Domain.Entities.ProgramProgress;

/// <summary>
/// Inscripción de un paciente en una plantilla de programa. Es el agregado raíz
/// del módulo: posee el estado de racha, el balance de XP, el puntero de semana
/// actual y la zona horaria del paciente. Toda la matemática de semanas/días es
/// local al paciente (<see cref="Timezone"/>).
/// </summary>
public sealed class ProgramEnrollment
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }

    public Guid TemplateId { get; set; }

    /// <summary>Zona horaria IANA del paciente (default <c>America/Bogota</c>).</summary>
    public string Timezone { get; set; } = "America/Bogota";

    public ProgramEnrollmentStatus Status { get; set; } = ProgramEnrollmentStatus.Active;

    /// <summary>Primer timestamp del servidor al crear la inscripción.</summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Lunes (en zona del paciente) de la semana 1.</summary>
    public DateOnly StartLocalDate { get; set; }

    private int _currentWeekNumber = 1;

    /// <summary>Semana actual: 0 mientras PendingAssignment; 1..TotalWeeks en curso.</summary>
    public int CurrentWeekNumber
    {
        get => Status == ProgramEnrollmentStatus.PendingAssignment ? 0 : _currentWeekNumber;
        set => _currentWeekNumber = value;
    }

    /// <summary>Se fija cuando current_week_number = total_weeks y la última semana pasa a Completed.</summary>
    public DateTime? CompletedAt { get; set; }

    public DateTime? PausedAt { get; set; }

    public DateTime? WithdrawnAt { get; set; }

    /// <summary>Clínico que inscribió al paciente (auditoría, sin navegación EF).</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>Último usuario que modificó la inscripción (auditoría, sin navegación EF).</summary>
    public Guid? UpdatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public PatientProfile? Patient { get; set; }

    public ProgramTemplate? Template { get; set; }

    public StreakState? StreakState { get; set; }

    public ICollection<ProgramWeek> Weeks { get; set; } = [];

    public ICollection<DailyCheckIn> DailyCheckIns { get; set; } = [];

    public ICollection<TaskCompletion> TaskCompletions { get; set; } = [];

    public ICollection<XpLedgerEntry> XpLedgerEntries { get; set; } = [];

    public ICollection<StreakFreeze> StreakFreezes { get; set; } = [];

    public ICollection<AdaptationRecommendation> Adaptations { get; set; } = [];

    public ICollection<EmotionalRecord> EmotionalRecords { get; set; } = [];
}