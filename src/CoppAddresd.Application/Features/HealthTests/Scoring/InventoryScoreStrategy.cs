using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Application.Features.HealthTests.Scoring;

/// <summary>
/// Estrategia <c>inventory</c>: cuenta señales/chips seleccionados (preguntas
/// de selección múltiple como historia clínica o flags de sueño). Cada opción
/// elegida aporta su <c>score_value</c> (por defecto 1 si no está definido);
/// el maxScore es la suma de los valores máximos por pregunta.
/// </summary>
public sealed class InventoryScoreStrategy : BaseScoreStrategy, IScoreStrategy
{
    public HealthTestScoringStrategy Strategy => HealthTestScoringStrategy.inventory;

    public ScoringOutput Calculate(
        IReadOnlyList<ScoreQuestion> questions,
        IReadOnlyList<ScoreAnswer> answers
    )
    {
        var byQuestion = IndexAnswers(answers);
        var total = 0m;
        var max = 0m;

        foreach (var q in questions)
        {
            if (q.Type != HealthTestQuestionType.multi)
            {
                continue;
            }

            // En multi-select se pueden marcar varias opciones: el máximo es la
            // suma de los valores de todas las opciones de la pregunta.
            max += q
                .Options.Where(o => o.ScoreValue.HasValue)
                .Select(o => o.ScoreValue!.Value)
                .DefaultIfEmpty(0)
                .Sum();

            // Para multi, el paciente puede haber respondido varias veces a la
            // misma pregunta (una fila por opción). Se suman las opciones elegidas.
            var selected = answers
                .Where(a => a.QuestionId == q.Id && a.AnswerOptionId.HasValue)
                .Select(a => q.Options.FirstOrDefault(o => o.Id == a.AnswerOptionId!.Value))
                .Where(o => o is not null)
                .ToList();

            foreach (var option in selected)
            {
                if (option is not null)
                {
                    total += option.ScoreValue ?? 1m;
                }
            }
        }

        return new ScoringOutput(total, max, []);
    }
}
