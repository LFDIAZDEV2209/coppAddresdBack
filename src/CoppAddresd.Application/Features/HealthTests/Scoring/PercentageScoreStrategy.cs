using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Application.Features.HealthTests.Scoring;

/// <summary>
/// Estrategia <c>percentage</c>: igual que <c>sum</c> pero el score se normaliza
/// a 0-100 (<c>score / max * 100</c>). Útil para instrumentos cuyo resultado se
/// reporta como porcentaje (p. ej. IAC-ADRESD como índice de adherencia).
/// </summary>
public sealed class PercentageScoreStrategy : BaseScoreStrategy, IScoreStrategy
{
    public HealthTestScoringStrategy Strategy => HealthTestScoringStrategy.percentage;

    public ScoringOutput Calculate(
        IReadOnlyList<ScoreQuestion> questions,
        IReadOnlyList<ScoreAnswer> answers
    )
    {
        var byQuestion = IndexAnswers(answers);
        var max = MaxFor(questions);
        var sum = SumFor(questions, byQuestion);
        var score = max > 0 ? Math.Round(sum / max * 100m, 2) : 0m;
        return new ScoringOutput(score, 100m, []);
    }
}
