namespace CoppAddresd.Domain.Enums.ProgramProgress;

/// <summary>
/// Categorías de debilidad del paciente (SPEC §21, "Paso 7c"): el eje funcional
/// que la regla evalúa. Los nombres del enum son exactamente los valores
/// almacenados (<c>varchar(40)</c> en <c>app.weaknesses.category</c>):
/// <c>nutritional</c>, <c>clinical</c>, <c>psychological</c>, <c>exercise</c>,
/// <c>adherence</c>, <c>supplement</c>, <c>sleep</c> y <c>motivation</c>.
/// Convención de nombres en minúscula igual que <see cref="TaskCode"/>.
/// </summary>
public enum WeaknessCategory
{
    /// <summary>Nutrición / alimentación (adherencia a comidas e hidratación).</summary>
    nutritional = 1,

    /// <summary>Métricas clínicas (glucosa, % grasa corporal, etc.).</summary>
    clinical = 2,

    /// <summary>Bienestar psicológico (ánimo, estrés, motivación).</summary>
    psychological = 3,

    /// <summary>Actividad física (cumplimiento de la tarea ejercicio).</summary>
    exercise = 4,

    /// <summary>Adherencia general al programa (racha, semana).</summary>
    adherence = 5,

    /// <summary>Suplemento / nutribiótico (constancia de la toma).</summary>
    supplement = 6,

    /// <summary>Sueño (horas promedio).</summary>
    sleep = 7,

    /// <summary>Motivación (proxy psicométrico del registro emocional).</summary>
    motivation = 8,
}