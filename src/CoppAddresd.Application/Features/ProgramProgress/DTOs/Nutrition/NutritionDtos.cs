using System.Text.Json.Serialization;

namespace CoppAddresd.Application.Features.ProgramProgress.DTOs.Nutrition;

/// <summary>
/// Payload opcional de intake nutricional de <c>POST /api/v1/program/nutrition/log</c>
/// (SPEC nutrition-intake-adherence): extiende <c>{ mealCode, localDate }</c> con
/// 8 campos opcionales de intake. El cliente NUNCA envía contexto de plan
/// (patientId y plan-day se resuelven server-side). Un payload sin intake
/// (o todos los campos null) sigue persistiendo el marcador de comida.
///
/// ================= CONTRATO FOOD-AI (documentado, sin integración runtime) =================
/// Forma esperada de <c>POST /api/v1/foodai/analyze</c> (espejo tipado de
/// <c>FoodAnalysisResult</c> en <c>foodAiApi.ts:45-55</c> del móvil):
///
/// <code>
/// {
///   "analysisId": "uuid",            // id público del análisis (idempotente)
///   "status": "completed",           // estado del pipeline
///   "modelVersion": "v1",            // versión del modelo usado
///   "foods": [                       // ítems reconocidos en la foto
///     {
///       "name": "pollo a la plancha",
///       "confidence": 0.92,
///       "portion": "150g",
///       "nutrition": { "calories": 225, "protein": 32, "carbohydrates": 0,
///                      "fat": 9, "fiber": 0, "sugar": 0, "sodium": 410 },
///       "nutritionStatus": "balanced"
///     }
///   ],
///   "summary": {                     // totales del análisis (fuente del intake)
///     "calories": 450, "protein": 25.5, "carbohydrates": 40,
///     "fat": 12, "fiber": 6, "sugar": 3, "sodium": 820
///   }
/// }
/// </code>
///
/// Vinculación: el cliente usa <c>summary</c> (o la suma de ítems) para los
/// campos de intake y envía <c>source: "ai_photo"</c> + <c>foodAnalysisId</c>
/// UNA vez por comida seleccionada. UN análisis confirmado se vincula a UNA
/// comida = UNA fila de intake (una fila por comida/día, tope de XP de 4
/// comidas); el split por ítem queda documentado como evolución futura.
/// Nota W2 (first-write-wins): si la comida ya fue registrada manualmente, el
/// log con el análisis devuelve 409 y los macros de la foto se descartan —
/// aceptado para v1.
/// =======================================================================================
/// </summary>
public sealed record NutritionIntakePayload(
    [property: JsonPropertyName("calories")] int? Calories,
    [property: JsonPropertyName("proteinG")] decimal? ProteinG,
    [property: JsonPropertyName("carbsG")] decimal? CarbsG,
    [property: JsonPropertyName("fatG")] decimal? FatG,
    [property: JsonPropertyName("fiberG")] decimal? FiberG,
    [property: JsonPropertyName("waterMl")] int? WaterMl,
    [property: JsonPropertyName("source")] string? Source,
    [property: JsonPropertyName("foodAnalysisId")] Guid? FoodAnalysisId);

/// <summary>
/// Respuesta de <c>POST /api/v1/program/nutrition/log</c> (SPEC §18, B): el
/// <c>habit_check</c> creado/actualizado y la XP granular otorgada. Un replay
/// idempotente de una COMIDA devuelve 409 <c>HABIT_ALREADY_LOGGED</c> (la
/// comida de la fecha ya fue registrada); el AGUA acumula — la repetición del
/// mismo día responde 200 con <c>xpAwarded = 0</c> tras actualizar el total
/// acumulado <c>waterMl</c>. La XP nunca se duplica (dedupe parcial del libro
/// mayor con <c>source_ref_type = 'habit_log'</c>).
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