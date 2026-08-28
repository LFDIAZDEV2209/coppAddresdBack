using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Domain.Entities.ProgramProgress;

/// <summary>
/// Fila histórica del Índice de Salud (SPEC §13.1.3): puntaje ponderado de las
/// 5 dimensiones para un período local del paciente. Se persiste al leer
/// (compute-on-read, §13.3): la fila del período anterior es el
/// <c>score_previous</c> de la siguiente, y el <c>trend</c> se deriva de la
/// comparación. Una fila por <c>(patient_id, period_start, period_end)</c>.
/// </summary>
public sealed class HealthScore
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }

    /// <summary>Puntaje ponderado redondeado (0..100).</summary>
    public int Score { get; set; }

    /// <summary>Puntaje del período anterior persistido (null en el primero).</summary>
    public int? ScorePrevious { get; set; }

    public int ScoreAdherence { get; set; }

    public int ScoreClinical { get; set; }

    public int ScoreNutrition { get; set; }

    public int ScorePsychology { get; set; }

    public int ScoreExercise { get; set; }

    /// <summary>Tendencia derivada de <c>Score</c> vs <c>ScorePrevious</c>.</summary>
    public ScoreTrend Trend { get; set; }

    /// <summary>Inicio del período local (ventana rodante de 7 días o semana del programa, §13.2).</summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>Fin del período local.</summary>
    public DateOnly PeriodEnd { get; set; }

    /// <summary>Reloj del servidor al calcular.</summary>
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public PatientProfile? Patient { get; set; }
}