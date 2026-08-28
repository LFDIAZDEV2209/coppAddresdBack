namespace CoppAddresd.Domain.Enums.ProgramProgress;

/// <summary>
/// Tipo de intervención derivada de una debilidad (SPEC §22, "Paso 7d"):
/// mapea la acción sugerida del motor de reglas (SPEC §21, B) a un tipo
/// de intervención concreto. Los nombres son los valores exactos almacenados
/// como <c>varchar(60)</c> en <c>app.interventions.type</c>.
/// </summary>
public enum InterventionType
{
    /// <summary>Ajuste del plan nutricional (acción: <c>create_intervention</c> para nutrición).</summary>
    nutrition_adjustment = 1,

    /// <summary>Ajuste del plan de ejercicio (acción: <c>create_intervention</c> para ejercicio).</summary>
    exercise_adjustment = 2,

    /// <summary>Apoyo psicológico (acción: <c>create_intervention</c> para psicología).</summary>
    psychological_support = 3,

    /// <summary>Teleconsulta con nutricionista (acción: <c>telehealth_referral</c> → nutricionista).</summary>
    telehealth_nutrition = 4,

    /// <summary>Teleconsulta con médico (acción: <c>referral_doctor</c>).</summary>
    telehealth_medical = 5,

    /// <summary>Teleconsulta con psicólogo (acción: <c>referral_psychologist</c>).</summary>
    telehealth_psychology = 6,

    /// <summary>Misión de recuperación (acción: <c>recovery_mode</c>).</summary>
    recovery_mission = 7,

    /// <summary>Adaptación del plan del programa (acción: <c>rto_adjustment</c> o <c>ai_recommendation</c>).</summary>
    plan_adaptation = 8,
}
