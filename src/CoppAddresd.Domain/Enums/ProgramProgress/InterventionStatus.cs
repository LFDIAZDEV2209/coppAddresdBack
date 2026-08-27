namespace CoppAddresd.Domain.Enums.ProgramProgress;

/// <summary>
/// Ciclo de vida de una intervención (SPEC §22, "Paso 7d"): la máquina de
/// estados que gobierna la transición de una intervención desde su creación
/// (derivada de una debilidad o por acción clínica) hasta su cierre.
/// Los nombres son los valores exactos almacenados como <c>varchar(30)</c>
/// en <c>app.interventions.status</c>, default <c>detected</c>.
///
/// Máquina de estados:
/// <c>detected</c> → <c>evaluated</c> → <c>recommended</c> → <c>accepted</c>
/// → <c>in_progress</c> → <c>completed</c> | <c>reevaluation</c>
///
/// El paciente acepta (<c>detected</c> → <c>accepted</c>); el clínico gestiona
/// las demás transiciones. Una transición a un estado inválido devuelve
/// <c>409 CONFLICT</c>.
/// </summary>
public enum InterventionStatus
{
    /// <summary>Intervención detectada por el motor de debilidades o creada por un profesional.</summary>
    detected = 1,

    /// <summary>Clínico evaluó la intervención y determinó el siguiente paso.</summary>
    evaluated = 2,

    /// <summary>Clínico recomendó la intervención al paciente (pendiente de aceptación).</summary>
    recommended = 3,

    /// <summary>Paciente aceptó la intervención (detected→accepted o recommended→accepted).</summary>
    accepted = 4,

    /// <summary>La intervención está en curso (ej. teleconsulta agendada o en progreso).</summary>
    in_progress = 5,

    /// <summary>Intervención completada con resultado positivo.</summary>
    completed = 6,

    /// <summary>Reevaluación solicitada por el clínico (ciclo de retroalimentación).</summary>
    reevaluation = 7,
}
