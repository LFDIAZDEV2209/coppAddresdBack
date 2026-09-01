namespace CoppAddresd.Domain.Enums.HealthTests;

/// <summary>
/// Ciclo de vida de una versión de instrumento (SPEC A10): <c>draft</c> (en
/// edición, no asignable), <c>active</c> (publicada, es la que reciben nuevas
/// asignaciones) y <c>retired</c> (reemplazada; las evaluaciones existentes la
/// conservan). Se almacena como <c>varchar</c> con CHECK.
/// </summary>
public enum HealthTestVersionStatus
{
    draft = 1,
    active = 2,
    retired = 3,
}

/// <summary>
/// Estados de una asignación de test a un paciente (SPEC A7): <c>pending</c>
/// (creada, esperando inicio), <c>in_progress</c> (evaluación iniciada),
/// <c>completed</c> (evaluación entregada), <c>expired</c> (venció sin
/// completarse) y <c>cancelled</c> (anulada por el profesional).
/// </summary>
public enum HealthTestAssignmentStatus
{
    pending = 1,
    in_progress = 2,
    completed = 3,
    expired = 4,
    cancelled = 5,
}

/// <summary>
/// Estados de una ejecución concreta de una versión (SPEC A8): <c>started</c>
/// (en curso, respuestas parciales), <c>completed</c> (entregada) y
/// <c>abandoned</c> (descartada sin completar).
/// </summary>
public enum HealthTestEvaluationStatus
{
    started = 1,
    completed = 2,
    abandoned = 3,
}

/// <summary>
/// Estados de una alerta clínica (SPEC A10): <c>active</c>, <c>reviewing</c>,
/// <c>resolved</c> y <c>closed</c>.
/// </summary>
public enum HealthTestAlertStatus
{
    active = 1,
    reviewing = 2,
    resolved = 3,
    closed = 4,
}

/// <summary>
/// Tipo de pregunta de un instrumento (SPEC A3): <c>scale</c> (escala Likert),
/// <c>single</c> (selección única), <c>multi</c> (selección múltiple),
/// <c>open</c> (texto libre) y <c>num</c> (valor numérico con unidad/rango;
/// no puntúa, su respuesta viaja como <c>value_text</c>).
/// </summary>
public enum HealthTestQuestionType
{
    scale = 1,
    single = 2,
    multi = 3,
    open = 4,
    num = 5,
}

/// <summary>
/// Dirección de scoring de una pregunta (SPEC A9): <c>positive</c> (a mayor
/// valor de opción, mayor score) o <c>reverse</c> (invierte: p. ej. ítems
/// redactados en negativo como "consumo ultraprocesados con frecuencia").
/// </summary>
public enum HealthTestScoringDirection
{
    positive = 1,
    reverse = 2,
}

/// <summary>
/// Tipo de resultado derivado de una evaluación (SPEC A11): <c>score</c>
/// (score total del instrumento), <c>subscale</c> (score por sección/subescala)
/// e <c>indicator</c> (indicador derivado por fórmula configurable).
/// </summary>
public enum HealthTestResultType
{
    score = 1,
    subscale = 2,
    indicator = 3,
}

/// <summary>
/// Severidad de un resultado o alerta (SPEC A12): <c>low</c>, <c>moderate</c>,
/// <c>high</c> y <c>critical</c>.
/// </summary>
public enum HealthTestSeverity
{
    low = 1,
    moderate = 2,
    high = 3,
    critical = 4,
}

/// <summary>
/// Estrategia de scoring de una versión (SPEC A9). Es el único vocabulario
/// que exige switch en código: cada valor corresponde a una implementación
/// registrada de <c>IScoreStrategy</c>. Agregar una estrategia nueva requiere
/// una clase nueva + test, no una migración.
/// </summary>
public enum HealthTestScoringStrategy
{
    sum = 1,
    percentage = 2,
    subscale = 3,
    inventory = 4,
    weighted = 5,
}
