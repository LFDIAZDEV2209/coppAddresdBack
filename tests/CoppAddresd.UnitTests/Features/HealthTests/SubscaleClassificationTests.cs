using CoppAddresd.Application.Features.HealthTests;
using CoppAddresd.Application.Features.HealthTests.Execution;
using CoppAddresd.Application.Features.HealthTests.Scoring;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CoppAddresd.UnitTests.Features.HealthTests;

public sealed class SubscaleClassificationTests
{
    [Fact]
    public void Alert_SeveridadRequerida_NoCoincideConDimensionSinClasificar()
    {
        var engine = new AlertEngine();
        var condition = engine.Parse("""{"when":{"resultType":"subscale","code":"dimension","severity":["high"]}}""");
        var result = new HealthTestResult
        {
            ResultType = HealthTestResultType.subscale, Code = "dimension", Value = 4,
        };
        Assert.False(engine.Matches(condition, result));
        result.Severity = HealthTestSeverity.high;
        Assert.True(engine.Matches(condition, result));
    }

    [Fact]
    public async Task Submit_TotalAlto_NoAplicaRangosDelTotalASubescalas()
    {
        var version = new HealthTestVersion
        {
            Id = Guid.NewGuid(),
            Instrument = new HealthTestInstrument { Code = "temperamento", Name = "Temperamento" },
            ScoringStrategy = HealthTestScoringStrategy.subscale,
            ScoreRanges =
            [
                new() { MinValue = 0, MaxValue = 7, Label = "bajo", Severity = HealthTestSeverity.low, IsActive = true },
                new() { MinValue = 8, MaxValue = 14, Label = "moderado", Severity = HealthTestSeverity.moderate, IsActive = true },
                new() { MinValue = 15, MaxValue = 22, Label = "alto", Severity = HealthTestSeverity.high, IsActive = true },
            ],
        };
        int[] values = [3, 2, 4, 4, 3, 4];
        int[] maxima = [4, 3, 4, 4, 3, 4];
        var answers = new List<SubmitAnswerInput>();
        for (var i = 0; i < values.Length; i++)
        {
            var question = new HealthTestQuestion
            {
                Id = Guid.NewGuid(), Code = $"dimension-{i}", Section = $"dimension-{i}",
                Type = HealthTestQuestionType.scale,
                Options = Enumerable.Range(0, maxima[i] + 1)
                    .Select(value => new HealthTestAnswerOption { Id = Guid.NewGuid(), ScoreValue = value })
                    .ToList(),
            };
            version.Questions.Add(question);
            answers.Add(new(question.Id, question.Options.Single(o => o.ScoreValue == values[i]).Id, null));
        }
        var assignment = new HealthTestAssignment
        {
            Id = Guid.NewGuid(), PatientId = Guid.NewGuid(), VersionId = version.Id,
            Status = HealthTestAssignmentStatus.in_progress,
        };
        var evaluation = new HealthTestEvaluation
        {
            Id = Guid.NewGuid(), AssignmentId = assignment.Id, PatientId = assignment.PatientId,
            VersionId = version.Id, Version = version, Status = HealthTestEvaluationStatus.started,
        };
        var repository = new Mock<IHealthTestRepository>();
        repository.Setup(r => r.GetAssignmentByIdAsync(assignment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assignment);
        repository.Setup(r => r.GetEvaluationByAssignmentAsync(assignment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(evaluation.Id);
        repository.Setup(r => r.GetEvaluationWithDetailsAsync(evaluation.Id, It.IsAny<CancellationToken>())).ReturnsAsync(evaluation);
        repository.Setup(r => r.GetVersionWithDetailsAsync(version.Id, It.IsAny<CancellationToken>())).ReturnsAsync(version);
        repository.Setup(r => r.ListResponsesByEvaluationAsync(evaluation.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<HealthTestResponse>());
        repository.Setup(r => r.ListActiveIndicatorDefsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<HealthTestIndicatorDef>());
        repository.Setup(r => r.ListActiveAlertRulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<HealthTestAlertRule>());
        repository.Setup(r => r.ExecuteInTransactionAsync(It.IsAny<Func<Task<HealthTestEvaluationDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task<HealthTestEvaluationDto>> action, CancellationToken _) => action());
        repository.Setup(r => r.AddResultsRangeAsync(It.IsAny<IEnumerable<HealthTestResult>>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<HealthTestResult> results, CancellationToken _) => evaluation.Results = results.ToList())
            .Returns(Task.CompletedTask);
        using var services = new ServiceCollection()
            .AddKeyedSingleton<IScoreStrategy, SubscaleScoreStrategy>(HealthTestScoringStrategy.subscale)
            .BuildServiceProvider();
        var handler = new SubmitEvaluationCommandHandler(repository.Object, new ScoreStrategyRegistry(services),
            new ScoreRangeEngine(), new IndicatorEngine(), new AlertEngine());

        var dto = await handler.Handle(new(assignment.Id, new(answers)), CancellationToken.None);

        var total = Assert.Single(evaluation.Results, r => r.ResultType == HealthTestResultType.score);
        Assert.Equal(20m, total.Value);
        Assert.Equal("alto", total.Qualifier);
        Assert.Equal(HealthTestSeverity.high, total.Severity);
        Assert.Equal(90.91m, dto.ScorePercentage);
        var dimensions = evaluation.Results.Where(r => r.ResultType == HealthTestResultType.subscale).ToList();
        Assert.Equal(values.Select(v => (decimal)v), dimensions.Select(r => r.Value));
        Assert.All(dimensions, r => { Assert.Null(r.Qualifier); Assert.Null(r.Severity); });
        Assert.All(dto.Results.Where(r => r.ResultType == HealthTestResultType.subscale),
            r => { Assert.Null(r.Qualifier); Assert.Null(r.Severity); });
    }

    [Theory]
    [InlineData(HealthTestResultType.subscale, true)]
    [InlineData(HealthTestResultType.score, false)]
    [InlineData(HealthTestResultType.indicator, false)]
    public void FromEntity_Historico_OmiteSoloClasificacionDeSubescalaSinAlterarSnapshot(
        HealthTestResultType type, bool omitClassification)
    {
        var saved = new HealthTestResult
        {
            Id = Guid.NewGuid(), EvaluationId = Guid.NewGuid(), ResultType = type,
            Code = "dimension", Label = "Dimensión", Value = 4, Qualifier = "bajo", Severity = HealthTestSeverity.low,
        };
        var dto = HealthTestResultDto.FromEntity(saved);
        Assert.Equal(saved.Id, dto.Id);
        Assert.Equal(saved.EvaluationId, dto.EvaluationId);
        Assert.Equal(4m, dto.Value);
        Assert.Equal(omitClassification ? null : "bajo", dto.Qualifier);
        Assert.Equal(omitClassification ? (HealthTestSeverity?)null : HealthTestSeverity.low, dto.Severity);
        Assert.Equal("bajo", saved.Qualifier);
        Assert.Equal(HealthTestSeverity.low, saved.Severity);
    }
}
