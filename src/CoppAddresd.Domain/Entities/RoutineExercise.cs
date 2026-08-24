namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Ejercicio individual dentro de una rutina. Contiene la descripción del ejercicio,
/// parámetros (series, repeticiones, descanso) y opcionalmente un media demo.
/// </summary>
public sealed class RoutineExercise
{
    public Guid Id { get; set; }

    public Guid RoutineId { get; set; }

    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    /// <summary>Número de series (null si el ejercicio es por tiempo).</summary>
    public int? Sets { get; set; }

    /// <summary>Número de repeticiones por serie (null si es por tiempo).</summary>
    public int? Repetitions { get; set; }

    /// <summary>Descanso entre series en segundos.</summary>
    public int? RestSeconds { get; set; }

    /// <summary>Duración del ejercicio en segundos (para ejercicios basados en tiempo, ej. "30s de plancha").</summary>
    public int? DurationSecs { get; set; }

    /// <summary>Peso recomendado en kg (null si es peso corporal).</summary>
    public decimal? WeightKg { get; set; }

    /// <summary>Músculo objetivo de este ejercicio.</summary>
    public string? TargetMuscle { get; set; }

    /// <summary>Equipamiento para este ejercicio.</summary>
    public string? Equipment { get; set; }

    /// <summary>Tempo del ejercicio (ej: "2-1-2" = 2s bajar, 1s pausa, 2s subir).</summary>
    public string? Tempo { get; set; }

    /// <summary>Esfuerzo percibido (1-10).</summary>
    public int? Rpe { get; set; }

    /// <summary>Tips de forma/ejecución.</summary>
    public string? Tips { get; set; }

    /// <summary>FK opcional a MediaItem para video/imagen del ejercicio.</summary>
    public Guid? MediaId { get; set; }

    /// <summary>Orden del ejercicio dentro de la rutina.</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public ExerciseRoutine Routine { get; set; } = default!;

    public MediaItem? Media { get; set; }
}
