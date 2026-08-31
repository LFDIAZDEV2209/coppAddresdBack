using System.Text.Json;
using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Domain.Entities.ProgramProgress;

/// <summary>
/// Recomendación de adaptación visible para el clínico: un cambio propuesto
/// (dificultad, nivel, cambio de plantilla o refresco de contenido) que el
/// motor de reglas genera o el clínico propone manualmente.
/// </summary>
public sealed class AdaptationRecommendation
{
    public Guid Id { get; set; }

    public Guid EnrollmentId { get; set; }

    public AdaptationKind Kind { get; set; }

    /// <summary>Tipo de entidad objetivo (weekly_day_templates, program_enrollments, ...).</summary>
    public AdaptationTargetEntityType TargetEntityType { get; set; }

    public Guid TargetEntityId { get; set; }

    /// <summary>Descripción del cambio propuesto: nuevos puntos, nuevas FKs, valid_from/to, etc. (jsonb).</summary>
    public JsonElement Payload { get; set; }

    /// <summary>Motivo por el que el motor o el clínico propusieron el cambio.</summary>
    public string Reason { get; set; } = default!;

    public AdaptationStatus Status { get; set; } = AdaptationStatus.Pending;

    /// <summary>True para DifficultyChange, LevelChange, TemplateSwap; false para *ContentRefresh.</summary>
    public bool RequiresApproval { get; set; }

    /// <summary>Null cuando la genera el motor de reglas (auditoría, sin navegación EF).</summary>
    public Guid? RequestedBy { get; set; }

    /// <summary>Clínico que decidió la recomendación (auditoría, sin navegación EF).</summary>
    public Guid? DecidedBy { get; set; }

    public DateTime? DecidedAt { get; set; }

    public DateTime? AppliedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ProgramEnrollment? Enrollment { get; set; }
}