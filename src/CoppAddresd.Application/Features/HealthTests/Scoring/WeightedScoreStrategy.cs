using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Application.Features.HealthTests.Scoring;

/// <summary>
/// Estrategia <c>weighted</c>: normaliza cada sección a 0-100 y las combina con
/// peso igual (p. ej. IAC-ADRESD, donde cada dimensión debe pesar lo mismo
/// independientemente de su número de ítems). El score final es 0-100.
/// </summary>
public sealed class WeightedScoreStrategy : BaseScoreStrategy, IScoreStrategy
{
    public HealthTestScoringStrategy Strategy => HealthTestScoringStrategy.weighted;

    public ScoringOutput Calculate(
        IReadOnlyList<ScoreQuestion> questions,
        IReadOnlyList<ScoreAnswer> answers
    )
    {
        var byQuestion = IndexAnswers(answers);
        var sections = GroupBySection(questions);

        var subscales = new List<SubscaleScore>(sections.Count);
        var total = 0m;
        var weightedMax = 0m;
        foreach (var sectionGroup in sections)
        {
            var sectionQuestions = sectionGroup.Questions;
            var sectionMax = MaxFor(sectionQuestions);
            var sectionScore =
                sectionMax > 0 ? SumFor(sectionQuestions, byQuestion) / sectionMax * 100m : 0m;
            subscales.Add(new SubscaleScore(sectionGroup.Section, Math.Round(sectionScore, 2)));
            total += sectionScore;
            weightedMax += 100m;
        }

        var score = weightedMax > 0 ? Math.Round(total / weightedMax * 100m, 2) : 0m;
        return new ScoringOutput(score, 100m, subscales);
    }
}
