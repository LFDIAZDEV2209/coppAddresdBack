using CoppAddresd.Application.Features.HealthTests.Scoring;
using CoppAddresd.Domain.Enums.HealthTests;
using Xunit;

namespace CoppAddresd.UnitTests.Features.HealthTests;

/// <summary>
/// Pruebas unitarias del motor de scoring (SPEC A9): estrategias sum,
/// percentage, subscale, inventory y weighted, incluido el reverse scoring de
/// ítems en negativo y la dominancia de temperamento.
/// </summary>
public sealed class ScoreStrategyTests
{
    private static readonly Guid Q1 = Guid.NewGuid();
    private static readonly Guid Q2 = Guid.NewGuid();
    private static readonly Guid Q3 = Guid.NewGuid();
    private static readonly Guid Q4 = Guid.NewGuid();

    private static readonly Guid OptA1 = Guid.NewGuid();
    private static readonly Guid OptA2 = Guid.NewGuid();
    private static readonly Guid OptA3 = Guid.NewGuid();
    private static readonly Guid OptA4 = Guid.NewGuid();
    private static readonly Guid OptA5 = Guid.NewGuid();

    // Escala Likert 1-5 (temperamento/nutrición): opciones con valores 1..5.
    private static readonly IReadOnlyList<ScoreQuestion> Likert5Questions =
    [
        new(
            Q1,
            "q1",
            null,
            HealthTestQuestionType.scale,
            HealthTestScoringDirection.positive,
            [
                new ScoreOption(OptA1, 1),
                new ScoreOption(OptA2, 2),
                new ScoreOption(OptA3, 3),
                new ScoreOption(OptA4, 4),
                new ScoreOption(OptA5, 5),
            ]
        ),
        new(
            Q2,
            "q2",
            null,
            HealthTestQuestionType.scale,
            HealthTestScoringDirection.positive,
            [
                new ScoreOption(OptA1, 1),
                new ScoreOption(OptA2, 2),
                new ScoreOption(OptA3, 3),
                new ScoreOption(OptA4, 4),
                new ScoreOption(OptA5, 5),
            ]
        ),
    ];

    [Fact]
    public void Sum_EscalaLikert_SumaValoresDeOpciones()
    {
        var strategy = new SumScoreStrategy();
        var answers = new List<ScoreAnswer>
        {
            new(Q1, OptA4, null), // 4
            new(Q2, OptA5, null), // 5
        };

        var output = strategy.Calculate(Likert5Questions, answers);

        Assert.Equal(9m, output.Score);
        Assert.Equal(10m, output.MaxScore);
        Assert.Empty(output.Subscales);
    }

    [Fact]
    public void Reverse_ItemEnNegativo_InvierteValor()
    {
        // Pregunta en negativo: marcar 5 (frecuente, malo) debe invertirse a 1.
        var negative = new List<ScoreQuestion>
        {
            new(
                Q3,
                "q3",
                "riesgo",
                HealthTestQuestionType.scale,
                HealthTestScoringDirection.reverse,
                [
                    new ScoreOption(OptA1, 1),
                    new ScoreOption(OptA2, 2),
                    new ScoreOption(OptA3, 3),
                    new ScoreOption(OptA4, 4),
                    new ScoreOption(OptA5, 5),
                ]
            ),
        };
        var strategy = new SumScoreStrategy();

        var output = strategy.Calculate(negative, [new ScoreAnswer(Q3, OptA5, null)]);

        // 1 + 5 - 5 = 1
        Assert.Equal(1m, output.Score);
    }

    [Fact]
    public void Reverse_ValorMedio_SeMantiene()
    {
        var negative = new List<ScoreQuestion>
        {
            new(
                Q3,
                "q3",
                null,
                HealthTestQuestionType.scale,
                HealthTestScoringDirection.reverse,
                [
                    new ScoreOption(OptA1, 1),
                    new ScoreOption(OptA2, 2),
                    new ScoreOption(OptA3, 3),
                    new ScoreOption(OptA4, 4),
                    new ScoreOption(OptA5, 5),
                ]
            ),
        };
        var strategy = new SumScoreStrategy();

        var output = strategy.Calculate(negative, [new ScoreAnswer(Q3, OptA3, null)]);

        Assert.Equal(3m, output.Score);
    }

    [Fact]
    public void Percentage_NormalizaA100()
    {
        var strategy = new PercentageScoreStrategy();
        var answers = new List<ScoreAnswer>
        {
            new(Q1, OptA5, null), // 5
            new(Q2, OptA5, null), // 5
        };

        var output = strategy.Calculate(Likert5Questions, answers);

        Assert.Equal(100m, output.Score);
        Assert.Equal(100m, output.MaxScore);
    }

    [Fact]
    public void Subscale_CalculaPorSeccionYTotal()
    {
        var questions = new List<ScoreQuestion>
        {
            new(
                Q1,
                "q1",
                "sanguineo",
                HealthTestQuestionType.scale,
                HealthTestScoringDirection.positive,
                [new ScoreOption(OptA1, 1), new ScoreOption(OptA2, 2), new ScoreOption(OptA3, 3)]
            ),
            new(
                Q2,
                "q2",
                "sanguineo",
                HealthTestQuestionType.scale,
                HealthTestScoringDirection.positive,
                [new ScoreOption(OptA1, 1), new ScoreOption(OptA2, 2), new ScoreOption(OptA3, 3)]
            ),
            new(
                Q3,
                "q3",
                "flematico",
                HealthTestQuestionType.scale,
                HealthTestScoringDirection.positive,
                [new ScoreOption(OptA1, 1), new ScoreOption(OptA2, 2), new ScoreOption(OptA3, 3)]
            ),
        };
        var strategy = new SubscaleScoreStrategy();

        var output = strategy.Calculate(
            questions,
            [
                new ScoreAnswer(Q1, OptA3, null), // 3
                new ScoreAnswer(Q2, OptA2, null), // 2
                new ScoreAnswer(Q3, OptA1, null), // 1
            ]
        );

        Assert.Equal(6m, output.Score);
        Assert.Equal(9m, output.MaxScore);
        Assert.Equal(2, output.Subscales.Count);
        Assert.Contains(output.Subscales, s => s.Code == "sanguineo" && s.Value == 5m);
        Assert.Contains(output.Subscales, s => s.Code == "flematico" && s.Value == 1m);
    }

    [Fact]
    public void Subscale_DominanciaTemperamento_EligeSubescalaMayor()
    {
        // Dominancia: la subescala de mayor puntaje define el perfil.
        var questions = new List<ScoreQuestion>
        {
            new(
                Q1,
                "q1",
                "sanguineo",
                HealthTestQuestionType.scale,
                HealthTestScoringDirection.positive,
                [new ScoreOption(OptA1, 1), new ScoreOption(OptA2, 2), new ScoreOption(OptA3, 3)]
            ),
            new(
                Q2,
                "q2",
                "colerico",
                HealthTestQuestionType.scale,
                HealthTestScoringDirection.positive,
                [new ScoreOption(OptA1, 1), new ScoreOption(OptA2, 2), new ScoreOption(OptA3, 3)]
            ),
        };
        var strategy = new SubscaleScoreStrategy();

        var output = strategy.Calculate(
            questions,
            [
                new ScoreAnswer(Q1, OptA3, null), // sanguineo 3
                new ScoreAnswer(Q2, OptA1, null), // colerico 1
            ]
        );

        var dominant = output.Subscales.OrderByDescending(s => s.Value).First();
        Assert.Equal("sanguineo", dominant.Code);
        Assert.Equal(3m, dominant.Value);
    }

    [Fact]
    public void Inventory_ConteoDeSeñales_MultiSelect()
    {
        var questions = new List<ScoreQuestion>
        {
            new(
                Q1,
                "q1",
                null,
                HealthTestQuestionType.multi,
                HealthTestScoringDirection.positive,
                [new ScoreOption(OptA1, 1), new ScoreOption(OptA2, 1), new ScoreOption(OptA3, 1)]
            ),
        };
        var strategy = new InventoryScoreStrategy();

        // El paciente marca 2 chips de 3 (ej: 2 señales de apnea).
        var output = strategy.Calculate(
            questions,
            [new ScoreAnswer(Q1, OptA1, null), new ScoreAnswer(Q1, OptA2, null)]
        );

        Assert.Equal(2m, output.Score);
        Assert.Equal(3m, output.MaxScore);
    }

    [Fact]
    public void Inventory_SinSeleccion_ScoreCero()
    {
        var questions = new List<ScoreQuestion>
        {
            new(
                Q1,
                "q1",
                null,
                HealthTestQuestionType.multi,
                HealthTestScoringDirection.positive,
                [new ScoreOption(OptA1, 1), new ScoreOption(OptA2, 1)]
            ),
        };
        var strategy = new InventoryScoreStrategy();

        var output = strategy.Calculate(questions, []);

        Assert.Equal(0m, output.Score);
    }

    [Fact]
    public void Weighted_NormalizaCadaSeccionYPonderaIgual()
    {
        // Sección A: 3 preguntas (max 9); Sección B: 1 pregunta (max 3).
        var questions = new List<ScoreQuestion>
        {
            new(
                Q1,
                "q1",
                "motivacion",
                HealthTestQuestionType.scale,
                HealthTestScoringDirection.positive,
                [new ScoreOption(OptA1, 1), new ScoreOption(OptA2, 2), new ScoreOption(OptA3, 3)]
            ),
            new(
                Q2,
                "q2",
                "motivacion",
                HealthTestQuestionType.scale,
                HealthTestScoringDirection.positive,
                [new ScoreOption(OptA1, 1), new ScoreOption(OptA2, 2), new ScoreOption(OptA3, 3)]
            ),
            new(
                Q3,
                "q3",
                "motivacion",
                HealthTestQuestionType.scale,
                HealthTestScoringDirection.positive,
                [new ScoreOption(OptA1, 1), new ScoreOption(OptA2, 2), new ScoreOption(OptA3, 3)]
            ),
            new(
                Q4,
                "q4",
                "organizacion",
                HealthTestQuestionType.scale,
                HealthTestScoringDirection.positive,
                [new ScoreOption(OptA1, 1), new ScoreOption(OptA2, 2), new ScoreOption(OptA3, 3)]
            ),
        };
        var strategy = new WeightedScoreStrategy();

        var output = strategy.Calculate(
            questions,
            [
                new ScoreAnswer(Q1, OptA3, null), // motivacion 3/3 → 100
                new ScoreAnswer(Q2, OptA3, null), // motivacion 3/3 → 100
                new ScoreAnswer(Q3, OptA3, null), // motivacion 3/3 → 100
                new ScoreAnswer(Q4, OptA1, null), // organizacion 1/3 → 33.33
            ]
        );

        // Promedio de secciones: (100 + 33.33) / 2 ≈ 66.67
        Assert.Equal(66.67m, output.Score);
        Assert.Equal(2, output.Subscales.Count);
        Assert.Contains(output.Subscales, s => s.Code == "motivacion" && s.Value == 100m);
    }
}
