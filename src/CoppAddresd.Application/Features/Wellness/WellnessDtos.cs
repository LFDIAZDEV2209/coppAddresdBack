using System.Text.Json;
using CoppAddresd.Domain.Enums;

namespace CoppAddresd.Application.Features.Wellness;

// --- Nutrition Plan DTOs ---

public record NutritionPlanDto(
    Guid Id, string Name, string? Description, string? TargetCondition,
    int DurationDays, int? DailyCalorieTarget,
    decimal? DailyProteinTarget, decimal? DailyCarbsTarget,
    decimal? DailyFatTarget, decimal? DailyFiberTarget,
    string? Allergens, string? MealTiming,
    bool IsTemplate, Guid? PatientId, string? PatientName,
    Guid? SourcePlanId, NutritionPlanStatus Status,
    Guid? CreatedBy, DateTime CreatedAt, DateTime? UpdatedAt,
    IReadOnlyList<NutritionPlanDayDto> Days,
    Guid? AssignmentId = null)
{
    public static NutritionPlanDto FromEntity(Domain.Entities.NutritionPlan p, Guid? assignmentId = null) => new(
        p.Id, p.Name, p.Description, p.TargetCondition,
        p.DurationDays, p.DailyCalorieTarget,
        p.DailyProteinTarget, p.DailyCarbsTarget,
        p.DailyFatTarget, p.DailyFiberTarget,
        p.Allergens, p.MealTiming,
        p.IsTemplate, p.PatientId,
        p.Patient is null
            ? null
            : $"{p.Patient.FirstName} {p.Patient.LastName}".Trim(),
        p.SourcePlanId, p.Status,
        p.CreatedBy, p.CreatedAt, p.UpdatedAt,
        p.Days.Select(NutritionPlanDayDto.FromEntity).ToList(),
        assignmentId);
}

public record NutritionPlanListItemDto(
    Guid Id, string Name, string? Description, string? TargetCondition,
    int DurationDays, int? DailyCalorieTarget,
    decimal? DailyProteinTarget, decimal? DailyCarbsTarget,
    decimal? DailyFatTarget, decimal? DailyFiberTarget,
    string? Allergens, string? MealTiming,
    bool IsTemplate, Guid? PatientId, string? PatientName,
    NutritionPlanStatus Status, DateTime CreatedAt)
{
    public static NutritionPlanListItemDto FromEntity(Domain.Entities.NutritionPlan p) => new(
        p.Id, p.Name, p.Description, p.TargetCondition,
        p.DurationDays, p.DailyCalorieTarget,
        p.DailyProteinTarget, p.DailyCarbsTarget,
        p.DailyFatTarget, p.DailyFiberTarget,
        p.Allergens, p.MealTiming,
        p.IsTemplate, p.PatientId,
        p.Patient is null
            ? null
            : $"{p.Patient.FirstName} {p.Patient.LastName}".Trim(),
        p.Status, p.CreatedAt);
}

public record NutritionPlanDayDto(
    Guid Id, int DayNumber, MealType MealType,
    string? Description, string? Foods, int? Calories,
    decimal? ProteinG, decimal? CarbsG, decimal? FatG,
    decimal? FiberG, int? WaterMl, int DailyWaterMl,
    string? Notes, int SortOrder, Guid? MediaId)
{
    public static NutritionPlanDayDto FromEntity(Domain.Entities.NutritionPlanDay d) => new(
        d.Id, d.DayNumber, d.MealType,
        d.Description, d.Foods, d.Calories,
        d.ProteinG, d.CarbsG, d.FatG,
        d.FiberG, d.WaterMl, d.DailyWaterMl,
        d.Notes, d.SortOrder, d.MediaId);
}

public record CreateNutritionPlanRequest(
    string Name, string? Description, string? TargetCondition,
    int DurationDays, int? DailyCalorieTarget,
    decimal? DailyProteinTarget, decimal? DailyCarbsTarget,
    decimal? DailyFatTarget, decimal? DailyFiberTarget,
    string? Allergens, string? MealTiming,
    bool IsTemplate, Guid? PatientId, Guid? SourcePlanId,
    NutritionPlanStatus Status,
    IReadOnlyList<CreateNutritionPlanDayRequest>? Days);

public record CreateNutritionPlanDayRequest(
    int DayNumber, MealType MealType,
    string? Description, string? Foods, int? Calories,
    decimal? ProteinG, decimal? CarbsG, decimal? FatG,
    decimal? FiberG, int? WaterMl,
    string? Notes, int SortOrder, Guid? MediaId,
    int DailyWaterMl = 2000);

public record UpdateNutritionPlanRequest(
    string Name, string? Description, string? TargetCondition,
    int DurationDays, int? DailyCalorieTarget,
    decimal? DailyProteinTarget, decimal? DailyCarbsTarget,
    decimal? DailyFatTarget, decimal? DailyFiberTarget,
    string? Allergens, string? MealTiming,
    NutritionPlanStatus Status,
    IReadOnlyList<CreateNutritionPlanDayRequest>? Days);

public record PaginatedNutritionPlanResult(
    IReadOnlyList<NutritionPlanListItemDto> Data, int Total, int Page, int PageSize, int TotalPages);

// --- Exercise Routine DTOs ---

public record ExerciseRoutineDto(
    Guid Id, string Name, string? Description,
    RoutineDifficulty Difficulty, int? EstimatedMinutes,
    RoutineCategory Category, NutritionPlanStatus Status,
    string? TargetMuscles, string? Equipment,
    string? WarmupNotes, string? CooldownNotes,
    Guid? MediaId, Guid? CreatedBy,
    DateTime CreatedAt, DateTime? UpdatedAt,
    IReadOnlyList<RoutineExerciseDto> Exercises,
    Guid? AssignmentId = null)
{
    public static ExerciseRoutineDto FromEntity(Domain.Entities.ExerciseRoutine r, Guid? assignmentId = null) => new(
        r.Id, r.Name, r.Description,
        r.Difficulty, r.EstimatedMinutes,
        r.Category, r.Status,
        r.TargetMuscles, r.Equipment,
        r.WarmupNotes, r.CooldownNotes,
        r.MediaId, r.CreatedBy,
        r.CreatedAt, r.UpdatedAt,
        r.Exercises.Select(RoutineExerciseDto.FromEntity).ToList(),
        assignmentId);
}

public record ExerciseRoutineListItemDto(
    Guid Id, string Name, string? Description,
    RoutineDifficulty Difficulty, int? EstimatedMinutes,
    RoutineCategory Category, NutritionPlanStatus Status,
    string? TargetMuscles, string? Equipment,
    DateTime CreatedAt)
{
    public static ExerciseRoutineListItemDto FromEntity(Domain.Entities.ExerciseRoutine r) => new(
        r.Id, r.Name, r.Description,
        r.Difficulty, r.EstimatedMinutes,
        r.Category, r.Status,
        r.TargetMuscles, r.Equipment,
        r.CreatedAt);
}

public record RoutineExerciseDto(
    Guid Id, string Name, string? Description,
    int? Sets, int? Repetitions, int? RestSeconds,
    int? DurationSecs, decimal? WeightKg,
    string? TargetMuscle, string? Equipment,
    string? Tempo, int? Rpe, string? Tips,
    Guid? MediaId, int SortOrder)
{
    public static RoutineExerciseDto FromEntity(Domain.Entities.RoutineExercise e) => new(
        e.Id, e.Name, e.Description,
        e.Sets, e.Repetitions, e.RestSeconds,
        e.DurationSecs, e.WeightKg,
        e.TargetMuscle, e.Equipment,
        e.Tempo, e.Rpe, e.Tips,
        e.MediaId, e.SortOrder);
}

public record CreateExerciseRoutineRequest(
    string Name, string? Description,
    RoutineDifficulty Difficulty, int? EstimatedMinutes,
    RoutineCategory Category, NutritionPlanStatus Status,
    string? TargetMuscles, string? Equipment,
    string? WarmupNotes, string? CooldownNotes,
    Guid? MediaId,
    IReadOnlyList<CreateRoutineExerciseRequest>? Exercises,
    Guid? PatientId = null);

public record CreateRoutineExerciseRequest(
    string Name, string? Description,
    int? Sets, int? Repetitions, int? RestSeconds,
    int? DurationSecs, decimal? WeightKg,
    string? TargetMuscle, string? Equipment,
    string? Tempo, int? Rpe, string? Tips,
    Guid? MediaId, int SortOrder);

public record UpdateExerciseRoutineRequest(
    string Name, string? Description,
    RoutineDifficulty Difficulty, int? EstimatedMinutes,
    RoutineCategory Category, NutritionPlanStatus Status,
    string? TargetMuscles, string? Equipment,
    string? WarmupNotes, string? CooldownNotes,
    Guid? MediaId,
    IReadOnlyList<CreateRoutineExerciseRequest>? Exercises);

public record PaginatedExerciseRoutineResult(
    IReadOnlyList<ExerciseRoutineListItemDto> Data, int Total, int Page, int PageSize, int TotalPages);

// --- Routine Assignment DTOs ---

public record RoutineAssignmentDto(
    Guid Id, Guid PatientId, string? PatientName,
    Guid RoutineId, string? RoutineName,
    DateTime StartDate, DateTime? EndDate,
    AssignmentFrequency Frequency, AssignmentStatus Status,
    string? Notes, Guid? CreatedBy, DateTime CreatedAt)
{
    public static RoutineAssignmentDto FromEntity(Domain.Entities.RoutineAssignment a) => new(
        a.Id, a.PatientId, a.Patient?.FirstName + " " + a.Patient?.LastName,
        a.RoutineId, a.Routine?.Name,
        a.StartDate, a.EndDate,
        a.Frequency, a.Status,
        a.Notes, a.CreatedBy, a.CreatedAt);
}

public record CreateRoutineAssignmentRequest(
    Guid PatientId, Guid RoutineId,
    DateTime StartDate, DateTime? EndDate,
    AssignmentFrequency Frequency, AssignmentStatus Status,
    string? Notes);

public record UpdateRoutineAssignmentRequest(
    DateTime StartDate, DateTime? EndDate,
    AssignmentFrequency Frequency, AssignmentStatus Status,
    string? Notes);

public record PaginatedRoutineAssignmentResult(
    IReadOnlyList<RoutineAssignmentDto> Data, int Total, int Page, int PageSize, int TotalPages);

// --- Nutrition Plan Assignment DTOs ---

public record NutritionPlanAssignmentDto(
    Guid Id, Guid PatientId, string? PatientName,
    Guid PlanId, string? PlanName,
    DateTime StartDate, DateTime? EndDate,
    AssignmentStatus Status,
    string? Notes, Guid? CreatedBy, DateTime CreatedAt)
{
    public static NutritionPlanAssignmentDto FromEntity(Domain.Entities.NutritionPlanAssignment a) => new(
        a.Id, a.PatientId, a.Patient?.FirstName + " " + a.Patient?.LastName,
        a.PlanId, a.Plan?.Name,
        a.StartDate, a.EndDate,
        a.Status,
        a.Notes, a.CreatedBy, a.CreatedAt);
}

public record CreateNutritionPlanAssignmentRequest(
    Guid PatientId, Guid PlanId,
    DateTime StartDate, DateTime? EndDate,
    AssignmentStatus Status,
    string? Notes);

public record UpdateNutritionPlanAssignmentRequest(
    DateTime StartDate, DateTime? EndDate,
    AssignmentStatus Status,
    string? Notes);

public record PaginatedNutritionPlanAssignmentResult(
    IReadOnlyList<NutritionPlanAssignmentDto> Data, int Total, int Page, int PageSize, int TotalPages);

// --- AI Plan Generation DTOs ---

/// <summary>Solicitud de generación de un plan con IA: tipo de plan y paciente.</summary>
public record GeneratePlanRequest(Guid PatientId, string Type);

/// <summary>
/// Respuesta de generación de plan: el <see cref="Payload"/> es el JSON listo
/// para el formulario del frontend (mismo shape que
/// <see cref="CreateNutritionPlanRequest"/> o <see cref="CreateExerciseRoutineRequest"/>),
/// devuelto como <c>JsonElement</c> para no acoplar el contrato a los DTOs de
/// creación del módulo.
/// </summary>
public record GeneratePlanResponse(string Type, DateTime GeneratedAt, JsonElement Payload);

/// <summary>Respuesta del AI Service: tipo de plan y plan generado (JSON crudo).</summary>
public record AiPlanResult(string Type, JsonElement Plan);
