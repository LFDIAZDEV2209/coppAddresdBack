using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Application.Features.HealthTests.Scoring;

/// <summary>
/// Estrategia <c>sum</c>: score = suma de los valores efectivos de todas las
/// preguntas (con reverse scoring donde aplique). El maxScore es la suma de
/// los valores máximos de cada pregunta. No produce subescalas.
/// </summary>
public sealed class SumScoreStrategy : BaseScoreStrategy, IScoreStrategy
{
    public HealthTestScoringStrategy Strategy => HealthTestScoringStrategy.sum;

    public ScoringOutput Calculate(
        IReadOnlyList<ScoreQuestion> questions,
        IReadOnlyList<ScoreAnswer> answers
    )
    {
        var byQuestion = IndexAnswers(answers);
        var score = SumFor(questions, byQuestion);
        var max = MaxFor(questions);
        return new ScoringOutput(score, max, []);
    }
}
