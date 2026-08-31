namespace CoppAddresd.Domain.Enums.ProgramProgress;

/// <summary>
/// Severidad de una debilidad (SPEC §21): escala ordinal con la que el clínico
/// prioriza la cola de <c>app.weaknesses</c>. Los nombres del enum son
/// exactamente los valores almacenados (<c>varchar(20)</c> en
/// <c>app.weaknesses.severity</c>, default <c>low</c>): <c>low</c>,
/// <c>medium</c>, <c>high</c> y <c>critical</c>.
/// </summary>
public enum WeaknessSeverity
{
    /// <summary>Baja (p. ej. cumplimiento de ejercicio &lt; 60%).</summary>
    low = 1,

    /// <summary>Media (p. ej. adherencia nutricional &lt; 70%).</summary>
    medium = 2,

    /// <summary>Alta (p. ej. glucosa elevada o adherencia &lt; 50%).</summary>
    high = 3,

    /// <summary>Crítica (reservada para hallazgos que exigen acción inmediata).</summary>
    critical = 4,
}