using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Domain.Entities.ProgramProgress;

/// <summary>
/// Peso configurable de una dimensión del Índice de Salud (SPEC §13.1.1):
/// una fila por dimensión, única por <c>dimension</c>. El seeder inserta los
/// defaults <c>adherence=0.30, clinical=0.30, nutrition=0.20,
/// psychology=0.10, exercise=0.10</c> y la capa de aplicación valida
/// <c>SUM(weight) = 1.0000</c> en cualquier escritura (decisión 16 / AC-19).
/// </summary>
public sealed class HealthScoreWeight
{
    public Guid Id { get; set; }

    /// <summary>Dimensión del puntaje (<c>adherence</c>, <c>clinical</c>, ...).</summary>
    public ScoreDimension Dimension { get; set; }

    /// <summary>Peso de la dimensión en <c>numeric(5,4)</c> (0..1).</summary>
    public decimal Weight { get; set; }

    public string? Description { get; set; }

    /// <summary>Usuario de <c>auth.users</c> que creó el peso (auditoría, sin navegación EF).</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>Último usuario que modificó el peso (auditoría, sin navegación EF).</summary>
    public Guid? UpdatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}