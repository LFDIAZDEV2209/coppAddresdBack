using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Domain.Entities.ProgramProgress;

/// <summary>
/// Registro de intake nutricional de una comida/hidratación de un paciente en
/// una fecha local (SPEC nutrition-intake-adherence): una fila por
/// <c>(paciente, fecha local, mealCode)</c>, anclada 1:1 al <c>habit_check</c>
/// que el log granular ya crea. Todos los campos de intake son opcionales:
/// un marcador "baremo" persiste la fila con intake null y NO altera el
/// comportamiento de <c>habit_check</c> ni de XP (la XP granular sigue
/// viviendo en el flujo previo de <c>LogNutritionAsync</c>).
///
/// El <c>PatientId</c> proviene SOLO del JWT (nunca del cliente, anti-IDOR
/// AC-11). La referencia al plan-day se resuelve server-side desde la
/// asignación activa del paciente (misma resolución que el snapshot, D5); sin
/// plan activo la referencia queda null y no se eleva error.
///
/// <c>FoodAnalysisId</c> referencia la columna única
/// <c>foodai.food_analyses.analysis_id</c> (ownership verificada en el log
/// path, D6): un análisis confirmado por foto persiste con source
/// <c>ai_photo</c> en lugar de descartarse.
/// </summary>
public sealed class NutritionIntakeLog
{
    public Guid Id { get; set; }

    /// <summary>Paciente dueño del registro (resuelto del JWT, nunca del body).</summary>
    public Guid PatientId { get; set; }

    /// <summary>Ancla 1:1 al habit_check creado en la misma transacción.</summary>
    public Guid HabitCheckId { get; set; }

    /// <summary>Fecha local del paciente en que se registró la comida.</summary>
    public DateOnly LocalDate { get; set; }

    /// <summary>Código de comida/hidratación (des/alm/mer/cen/agua).</summary>
    public MealCode MealCode { get; set; }

    /// <summary>Calorías estimadas (kcal). Null si no se informaron.</summary>
    public int? Calories { get; set; }

    /// <summary>Gramos de proteína. Null si no se informaron.</summary>
    public decimal? ProteinG { get; set; }

    /// <summary>Gramos de carbohidratos. Null si no se informaron.</summary>
    public decimal? CarbsG { get; set; }

    /// <summary>Gramos de grasa. Null si no se informaron.</summary>
    public decimal? FatG { get; set; }

    /// <summary>Gramos de fibra. Null si no se informaron.</summary>
    public decimal? FiberG { get; set; }

    /// <summary>Mililitros de agua (solo hidratación). Null si no se informaron.</summary>
    public int? WaterMl { get; set; }

    /// <summary>
    /// Origen del registro: <c>manual</c> (entrada primaria del móvil) o
    /// <c>ai_photo</c> (análisis de foto confirmado). Default <c>manual</c>;
    /// sin CHECK (precedente de vital signs): valores libres.
    /// </summary>
    public string Source { get; set; } = "manual";

    /// <summary>
    /// Id público del análisis de comida (foodai) que originó los macros, si
    /// el registro vino de una foto confirmada. FK Restrict a
    /// <c>food_analyses.analysis_id</c>.
    /// </summary>
    public Guid? FoodAnalysisId { get; set; }

    /// <summary>Plan de alimentación activo resuelto server-side (null sin plan).</summary>
    public Guid? NutritionPlanId { get; set; }

    /// <summary>Día del plan (weekday ISO) resuelto server-side (null sin plan).</summary>
    public short? NutritionPlanDayNumber { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PatientProfile? Patient { get; set; }

    public HabitCheck? HabitCheck { get; set; }
}