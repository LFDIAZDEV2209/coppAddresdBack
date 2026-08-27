using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Application.Features.HealthTests.Scoring;

/// <summary>
/// Pregunta del instrumento tal como la consume el motor de scoring: dirección
/// de scoring y opciones con sus valores. Neutral a EF (fácil de construir en
/// tests y desde el repositorio).
/// </summary>
public sealed record ScoreQuestion(
    Guid Id,
    string Code,
    string? Section,
    HealthTestQuestionType Type,
    HealthTestScoringDirection Direction,
    IReadOnlyList<ScoreOption> Options
);

/// <summary>
/// Opción de respuesta tal como la consume el motor de scoring.
/// </summary>
public sealed record ScoreOption(Guid Id, decimal? ScoreValue);

/// <summary>
/// Respuesta dada por el paciente a una pregunta (neutral a EF).
/// </summary>
public sealed record ScoreAnswer(Guid QuestionId, Guid? AnswerOptionId, string? ValueText);

/// <summary>
/// Resultado de una subescala/sección calculado por una estrategia.
/// </summary>
public sealed record SubscaleScore(string Code, decimal Value);

/// <summary>
/// Resultado del cálculo de una evaluación.
/// </summary>
public sealed record ScoringOutput(
    decimal Score,
    decimal MaxScore,
    IReadOnlyList<SubscaleScore> Subscales
);

/// <summary>
/// Estrategia de scoring registrada en el registry (SPEC A9). Cada estrategia
/// es una clase sin estado con una implementación pura del cálculo.
/// </summary>
public interface IScoreStrategy
{
    HealthTestScoringStrategy Strategy { get; }

    /// <summary>
    /// Calcula el score de la evaluación. Recibe las preguntas de la versión
    /// y las respuestas del paciente (ya normalizadas a opciones). No tiene
    /// dependencias de EF: es puro y testeable en memoria.
    /// </summary>
    ScoringOutput Calculate(
        IReadOnlyList<ScoreQuestion> questions,
        IReadOnlyList<ScoreAnswer> answers
    );
}
