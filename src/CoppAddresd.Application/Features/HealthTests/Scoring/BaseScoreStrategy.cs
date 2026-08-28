using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Application.Features.HealthTests.Scoring;

/// <summary>
/// Base compartida de las estrategias de scoring. Centraliza:
/// (a) resolución de la opción elegida por pregunta;
/// (b) aplicación de la dirección de scoring (reverse invierte el valor
///     usando el rango de opciones de la pregunta: <c>min + max - value</c>);
/// (c) agregación por sección (subescalas).
/// </summary>
public abstract class BaseScoreStrategy
{
    /// <summary>
    /// Construye el diccionario respuesta→pregunta usado por <see cref="SumFor"/>.
    /// </summary>
    protected static IReadOnlyDictionary<Guid, ScoreAnswer> IndexAnswers(
        IReadOnlyList<ScoreAnswer> answers
    ) =>
        answers
            .Where(a => a.QuestionId != Guid.Empty)
            .GroupBy(a => a.QuestionId)
            .ToDictionary(g => g.Key, g => g.Last());

    /// <summary>
    /// Devuelve el valor efectivo de la respuesta a una pregunta, aplicando la
    /// dirección de scoring. Para preguntas abiertas devuelve null (no puntúan).
    /// </summary>
    protected static decimal? ResolveQuestionScore(ScoreQuestion q, ScoreAnswer a)
    {
        if (a.AnswerOptionId is not { } optionId)
        {
            return null;
        }

        var option = q.Options.FirstOrDefault(o => o.Id == optionId);
        if (option is null || option.ScoreValue is not { } raw)
        {
            return null;
        }

        // Reverse scoring: invierte el valor dentro del rango de la escala.
        if (q.Direction == HealthTestScoringDirection.reverse)
        {
            var values = q
                .Options.Where(o => o.ScoreValue.HasValue)
                .Select(o => o.ScoreValue!.Value)
                .ToList();
            if (values.Count == 0)
            {
                return null;
            }

            return values.Min() + values.Max() - raw;
        }

        return raw;
    }

    /// <summary>Agrupa las preguntas por sección (subescala) en el orden dado.</summary>
    protected static IReadOnlyList<SectionGroup> GroupBySection(
        IReadOnlyList<ScoreQuestion> questions
    )
    {
        var result = new List<SectionGroup>();
        foreach (
            var group in questions
                .Where(q => !string.IsNullOrWhiteSpace(q.Section))
                .GroupBy(q => q.Section!)
        )
        {
            result.Add(new SectionGroup(group.Key, group.ToList()));
        }

        return result;
    }

    /// <summary>Agrupación de preguntas por sección (subescala).</summary>
    protected sealed record SectionGroup(string Section, IReadOnlyList<ScoreQuestion> Questions);

    /// <summary>
    /// Calcula la suma de los valores efectivos (con reverse) de las preguntas
    /// dadas, según las respuestas indexadas por <c>QuestionId</c>.
    /// </summary>
    protected static decimal SumFor(
        IReadOnlyList<ScoreQuestion> questions,
        IReadOnlyDictionary<Guid, ScoreAnswer> answerByQuestion
    )
    {
        var total = 0m;
        foreach (var q in questions)
        {
            if (answerByQuestion.TryGetValue(q.Id, out var answer))
            {
                var score = ResolveQuestionScore(q, answer);
                if (score.HasValue)
                {
                    total += score.Value;
                }
            }
        }

        return total;
    }

    /// <summary>
    /// Máximo posible de la suma para un conjunto de preguntas (usado para el
    /// porcentaje y el maxScore del resultado).
    /// </summary>
    protected static decimal MaxFor(IReadOnlyList<ScoreQuestion> questions)
    {
        var total = 0m;
        foreach (var q in questions)
        {
            var maxOption = q
                .Options.Where(o => o.ScoreValue.HasValue)
                .Select(o => o.ScoreValue!.Value)
                .DefaultIfEmpty(0)
                .Max();
            total += maxOption;
        }

        return total;
    }
}
