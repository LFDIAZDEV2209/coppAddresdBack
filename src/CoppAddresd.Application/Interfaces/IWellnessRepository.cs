using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Repositorio del módulo Centro de Bienestar. Operaciones de persistencia
/// para planes de alimentación, rutinas de ejercicio y asignaciones.
/// </summary>
public interface IWellnessRepository
{
    // --- Nutrition Plans ---
    Task<NutritionPlan?> GetPlanByIdAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<NutritionPlan> Items, int Total)> ListPlansAsync(
        bool? isTemplate, string? search, string? status, Guid? patientId,
        int page, int pageSize, CancellationToken ct = default);
    Task<NutritionPlan> AddPlanAsync(NutritionPlan plan, CancellationToken ct = default);
    Task UpdatePlanAsync(NutritionPlan plan, CancellationToken ct = default);
    Task DeletePlanAsync(NutritionPlan plan, CancellationToken ct = default);

    /// <summary>
    /// Inserta el plan (con sus días) y su asignación al paciente en la misma
    /// transacción (evita estados intermedios: plan sin asignación si el
    /// segundo insert falla, o asignación huérfana).
    /// </summary>
    Task<NutritionPlanAssignment> AddPlanWithAssignmentAsync(
        NutritionPlan plan, NutritionPlanAssignment assignment, CancellationToken ct = default);

    // --- Nutrition Plan Days ---
    Task<IReadOnlyList<NutritionPlanDay>> ListPlanDaysAsync(Guid planId, CancellationToken ct = default);
    Task<NutritionPlanDay?> GetPlanDayByIdAsync(Guid id, CancellationToken ct = default);
    Task AddPlanDaysRangeAsync(IEnumerable<NutritionPlanDay> days, CancellationToken ct = default);
    Task DeletePlanDaysByPlanIdAsync(Guid planId, CancellationToken ct = default);

    // --- Exercise Routines ---
    Task<ExerciseRoutine?> GetRoutineByIdAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<ExerciseRoutine> Items, int Total)> ListRoutinesAsync(
        string? search, string? status, string? category,
        int page, int pageSize, CancellationToken ct = default);
    Task<ExerciseRoutine> AddRoutineAsync(ExerciseRoutine routine, CancellationToken ct = default);
    Task UpdateRoutineAsync(ExerciseRoutine routine, CancellationToken ct = default);
    Task DeleteRoutineAsync(ExerciseRoutine routine, CancellationToken ct = default);

    /// <summary>
    /// Inserta la rutina (con sus ejercicios) y su asignación al paciente en la
    /// misma transacción (evita estados intermedios: rutina sin asignación si
    /// el segundo insert falla, o asignación huérfana).
    /// </summary>
    Task<RoutineAssignment> AddRoutineWithAssignmentAsync(
        ExerciseRoutine routine, RoutineAssignment assignment, CancellationToken ct = default);

    // --- Routine Exercises ---
    Task<IReadOnlyList<RoutineExercise>> ListRoutineExercisesAsync(Guid routineId, CancellationToken ct = default);
    Task<RoutineExercise?> GetRoutineExerciseByIdAsync(Guid id, CancellationToken ct = default);
    Task AddRoutineExercisesRangeAsync(IEnumerable<RoutineExercise> exercises, CancellationToken ct = default);
    Task DeleteRoutineExercisesByRoutineIdAsync(Guid routineId, CancellationToken ct = default);

    // --- Routine Assignments ---
    Task<RoutineAssignment?> GetAssignmentByIdAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<RoutineAssignment> Items, int Total)> ListAssignmentsAsync(
        Guid? patientId, Guid? routineId, string? status,
        int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<RoutineAssignment>> ListAssignmentsByPatientAsync(Guid patientId, CancellationToken ct = default);
    Task<RoutineAssignment> AddAssignmentAsync(RoutineAssignment assignment, CancellationToken ct = default);
    Task UpdateAssignmentAsync(RoutineAssignment assignment, CancellationToken ct = default);
    Task DeleteAssignmentAsync(RoutineAssignment assignment, CancellationToken ct = default);

    // --- Nutrition Plan Assignments ---
    Task<NutritionPlanAssignment?> GetPlanAssignmentByIdAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<NutritionPlanAssignment> Items, int Total)> ListPlanAssignmentsAsync(
        Guid? patientId, Guid? planId, string? status,
        int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<NutritionPlanAssignment>> ListPlanAssignmentsByPatientAsync(Guid patientId, CancellationToken ct = default);
    Task<NutritionPlanAssignment> AddPlanAssignmentAsync(NutritionPlanAssignment assignment, CancellationToken ct = default);
    Task UpdatePlanAssignmentAsync(NutritionPlanAssignment assignment, CancellationToken ct = default);
    Task DeletePlanAssignmentAsync(NutritionPlanAssignment assignment, CancellationToken ct = default);
}
