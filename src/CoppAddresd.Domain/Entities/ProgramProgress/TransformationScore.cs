using System.Text.Json;
using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Domain.Entities.ProgramProgress;

/// <summary>
/// Fila histórica del Índice de Transformación (SPEC §13.1.4): promedio de los
/// puntajes por indicador clínico (línea base vs medición más reciente de la
/// semana) con el detalle <c>jsonb</c> por métrica. Una fila por
/// <c>(patient_id, week_number)</c> en la práctica (se reescribe al cambiar la
/// semana; sin restricción única en la BD, SPEC §13.1.4).
/// </summary>
public sealed class TransformationScore
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }

    /// <summary>Promedio de indicadores redondeado (0..100).</summary>
    public int Score { get; set; }

    public int? ScorePrevious { get; set; }

    /// <summary>Snapshot de <c>program_enrollments.current_week_number</c> al calcular.</summary>
    public int WeekNumber { get; set; }

    /// <summary>
    /// Detalle por métrica: <c>{ metricCode: { baseline, current, unit, delta,
    /// delta_pct, favorable, score } }</c>. <c>'{}'</c> cuando no hay
    /// indicadores (SPEC §13.5).
    /// </summary>
    public JsonElement Detail { get; set; }

    /// <summary>Tendencia global derivada de <c>Score</c> vs <c>ScorePrevious</c>.</summary>
    public ScoreTrend OverallTrend { get; set; }

    /// <summary>Reloj del servidor al calcular.</summary>
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public PatientProfile? Patient { get; set; }
}