using CoppAddresd.Domain.Enums;

namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Rutina de ejercicio template. Contiene una lista de ejercicios
/// que se pueden asignar a pacientes. Es reutilizable entre múltiples pacientes.
/// </summary>
public sealed class ExerciseRoutine
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    public RoutineDifficulty Difficulty { get; set; } = RoutineDifficulty.Moderado;

    /// <summary>Duración estimada de la rutina completa en minutos.</summary>
    public int? EstimatedMinutes { get; set; }

    public RoutineCategory Category { get; set; } = RoutineCategory.Mixta;

    public NutritionPlanStatus Status { get; set; } = NutritionPlanStatus.Draft;

    /// <summary>Grupos musculares objetivo (ej: "Piernas, Core").</summary>
    public string? TargetMuscles { get; set; }

    /// <summary>Equipamiento necesario (ej: "Mancuernas, banda elástica").</summary>
    public string? Equipment { get; set; }

    /// <summary>Instrucciones del calentamiento.</summary>
    public string? WarmupNotes { get; set; }

    /// <summary>Instrucciones del enfriamiento.</summary>
    public string? CooldownNotes { get; set; }

    /// <summary>FK opcional a MediaItem para video demo de la rutina completa.</summary>
    public Guid? MediaId { get; set; }

    /// <summary>Id del usuario que creó la rutina (profesional).</summary>
    public Guid? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public MediaItem? Media { get; set; }

    public ICollection<RoutineExercise> Exercises { get; set; } = [];
}
