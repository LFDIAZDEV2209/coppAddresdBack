using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Application.Features.HealthTests.Scoring;

/// <summary>
/// Estrategia <c>subscale</c>: calcula el score por sección (subescala) y el
/// total como suma de secciones. Cada sección produce un
/// <see cref="SubscaleScore"/> (p. ej. temperamento: 4 humores; ORP: dominios).
/// </summary>
public sealed class SubscaleScoreStrategy : BaseScoreStrategy, IScoreStrategy
{
    public HealthTestScoringStrategy Strategy => HealthTestScoringStrategy.subscale;

    public ScoringOutput Calculate(
        IReadOnlyList<ScoreQuestion> questions,
        IReadOnlyList<ScoreAnswer> answers
    )
    {
        var byQuestion = IndexAnswers(answers);
        var sections = GroupBySection(questions);

        var subscales = new List<SubscaleScore>(sections.Count);
        var total = 0m;
        var max = 0m;

        foreach (var sectionGroup in sections)
        {
            var sectionQuestions = sectionGroup.Questions;
            var sectionScore = SumFor(sectionQuestions, byQuestion);
            var sectionMax = MaxFor(sectionQuestions);
            subscales.Add(new SubscaleScore(sectionGroup.Section, sectionScore));
            total += sectionScore;
            max += sectionMax;
        }

        // Preguntas sin sección: se suman al total pero no generan subescala.
        var unscored = questions.Where(q => string.IsNullOrWhiteSpace(q.Section)).ToList();
        if (unscored.Count > 0)
        {
            total += SumFor(unscored, byQuestion);
            max += MaxFor(unscored);
        }

        return new ScoringOutput(total, max, subscales);
    }
}
