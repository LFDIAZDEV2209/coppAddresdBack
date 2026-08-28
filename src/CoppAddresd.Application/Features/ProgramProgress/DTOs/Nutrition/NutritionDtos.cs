using System.Text.Json.Serialization;

namespace CoppAddresd.Application.Features.ProgramProgress.DTOs.Nutrition;

/// <summary>
/// Respuesta de <c>POST /api/v1/program/nutrition/log</c> (SPEC §18, B): el
/// <c>habit_check</c> creado/actualizado y la XP granular otorgada. Un replay
/// idempotente devuelve 409 <c>HABIT_ALREADY_LOGGED</c> (la comida/hidratación
/// de la fecha ya fue registrada); la XP nunca se duplica (dedupe parcial del
/// libro mayor con <c>source_ref_type = 'habit_log'</c>).
/// </summary>
public sealed record NutritionLogResultDto(
    [property: JsonPropertyName("habitCheckId")] Guid HabitCheckId,
    [property: JsonPropertyName("mealCode")] string MealCode,
    [property: JsonPropertyName("localDate")] DateOnly LocalDate,
    [property: JsonPropertyName("isDone")] bool IsDone,
    [property: JsonPropertyName("xpAwarded")] int XpAwarded,
    [property: JsonPropertyName("xpBalanceAfter")] int XpBalanceAfter);

/// <summary>
/// Resumen de los otorgamientos semanales de nutrición (SPEC §18, C) evaluados
/// SOLO en <c>POST /scores/calculate</c> (nunca en <c>GET /scores</c>), para el
/// log estructurado del handler: XP otorgada en el período (dedupe parcial de
/// <c>xp_ledger</c> con <c>source_ref_type = 'nutrition_period'</c>) y qué
/// reglas de nutrición se otorgaron.
/// </summary>
public sealed record NutritionWeeklyAwardsResult(
    [property: JsonPropertyName("totalXpAwarded")] int TotalXpAwarded,
    [property: JsonPropertyName("awardedRules")] IReadOnlyList<string> AwardedRules)
{
    public static readonly NutritionWeeklyAwardsResult Empty = new(0, []);
}