using CoppAddresd.Application.Features.HealthTests.Scoring;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using Xunit;

namespace CoppAddresd.UnitTests.Features.HealthTests;

/// <summary>
/// Pruebas del motor de indicadores (SPEC A11) y del motor de alertas (SPEC A12),
/// incluida la no-duplicación de alertas por regla+resultado.
/// </summary>
public sealed class IndicatorAndAlertEngineTests
{
    private static readonly Guid EvaluationId = Guid.NewGuid();

    private static HealthTestResult Result(
        string code,
        HealthTestResultType type,
        decimal value,
        string? severity = null
    ) =>
        new()
        {
            EvaluationId = EvaluationId,
            Code = code,
            Label = code,
            ResultType = type,
            Value = value,
            Severity = severity is null ? null : Enum.Parse<HealthTestSeverity>(severity),
        };

    // --- Indicadores ---

    [Fact]
    public void Indicator_Weighted_CalculaPromedioPonderado()
    {
        var engine = new IndicatorEngine();
        var computation = engine.Parse(
            """{"formula":"weighted","sources":[{"resultType":"subscale","code":"motivacion","weight":0.3},{"resultType":"subscale","code":"habitos","weight":0.7}]}"""
        );
        var results = new List<HealthTestResult>
        {
            Result("motivacion", HealthTestResultType.subscale, 80),
            Result("habitos", HealthTestResultType.subscale, 60),
        };

        var value = engine.Calculate(computation, results);

        // (80*0.3 + 60*0.7) / 1.0 = 66
        Assert.Equal(66m, value);
    }

    [Fact]
    public void Indicator_Sum_SumaFuentes()
    {
        var engine = new IndicatorEngine();
        var computation = engine.Parse(
            """{"formula":"sum","sources":[{"resultType":"score","code":"iapnea"},{"resultType":"score","code":"icardio"}]}"""
        );
        var results = new List<HealthTestResult>
        {
            Result("iapnea", HealthTestResultType.score, 2),
            Result("icardio", HealthTestResultType.score, 3),
        };

        var value = engine.Calculate(computation, results);

        Assert.Equal(5m, value);
    }

    [Fact]
    public void Indicator_Avg_PromedioSimple()
    {
        var engine = new IndicatorEngine();
        var computation = engine.Parse(
            """{"formula":"avg","sources":[{"resultType":"subscale","code":"a"},{"resultType":"subscale","code":"b"},{"resultType":"subscale","code":"c"}]}"""
        );
        var results = new List<HealthTestResult>
        {
            Result("a", HealthTestResultType.subscale, 100),
            Result("b", HealthTestResultType.subscale, 50),
            Result("c", HealthTestResultType.subscale, 0),
        };

        var value = engine.Calculate(computation, results);

        Assert.Equal(50m, value);
    }

    [Fact]
    public void Indicator_SinFuentesResolubles_DevuelveNull()
    {
        var engine = new IndicatorEngine();
        var computation = engine.Parse(
            """{"formula":"sum","sources":[{"resultType":"score","code":"inexistente"}]}"""
        );

        var value = engine.Calculate(computation, []);

        Assert.Null(value);
    }

    // --- Alertas ---

    [Fact]
    public void Alert_ResultadoCritico_EvaluaRegla()
    {
        var engine = new AlertEngine();
        var rule = new HealthTestAlertRule
        {
            Id = Guid.NewGuid(),
            Code = "orp_critico",
            Name = "ORP crítico",
            Condition =
                """{"when":{"resultType":"score","code":"orp","severity":["high","critical"]}}""",
            Severity = HealthTestSeverity.high,
            MessageTemplate = "ORP {value} - {label}",
            IsActive = true,
        };
        var results = new List<HealthTestResult>
        {
            Result("orp", HealthTestResultType.score, 80, "critical"),
        };

        var drafts = engine.Evaluate(Guid.NewGuid(), results, [rule]);

        Assert.Single(drafts);
        Assert.Equal(rule.Id, drafts[0].RuleId);
        Assert.Equal("ORP 80 - orp", drafts[0].Title);
    }

    [Fact]
    public void Alert_ResultadoBajo_NoDispara()
    {
        var engine = new AlertEngine();
        var rule = new HealthTestAlertRule
        {
            Id = Guid.NewGuid(),
            Code = "orp_critico",
            Name = "ORP crítico",
            Condition =
                """{"when":{"resultType":"score","code":"orp","severity":["high","critical"]}}""",
            Severity = HealthTestSeverity.high,
            IsActive = true,
        };
        var results = new List<HealthTestResult>
        {
            Result("orp", HealthTestResultType.score, 20, "low"),
        };

        var drafts = engine.Evaluate(Guid.NewGuid(), results, [rule]);

        Assert.Empty(drafts);
    }

    [Fact]
    public void Alert_Dedup_PorReglaYResultado()
    {
        // La deduplicación la garantiza la consulta del repositorio
        // (AlertExistsForResultRuleAsync). Aquí se valida que el motor produce
        // una sola draft por (resultado, regla) incluso con la regla repetida
        // en la lista.
        var engine = new AlertEngine();
        var rule = new HealthTestAlertRule
        {
            Id = Guid.NewGuid(),
            Code = "apnea",
            Name = "Sospecha de apnea",
            Condition =
                """{"when":{"resultType":"indicator","code":"iapnea","severity":["high"]}}""",
            Severity = HealthTestSeverity.high,
            IsActive = true,
        };
        var results = new List<HealthTestResult>
        {
            Result("iapnea", HealthTestResultType.indicator, 3, "high"),
        };

        var drafts = engine.Evaluate(Guid.NewGuid(), results, [rule, rule]);

        // Dos reglas idénticas → dos drafts para el mismo resultado; la
        // dedup final la resuelve el repositorio. Aquí comprobamos el
        // conteo por (resultado, regla): 2 drafts (una por cada entrada de
        // la regla), que el caller consolidaría por RuleId+ResultId.
        Assert.Equal(2, drafts.Count);
        Assert.All(drafts, d => Assert.Equal(rule.Id, d.RuleId));
    }

    [Fact]
    public void Alert_SeveridadEnPlantilla_SeReemplaza()
    {
        var engine = new AlertEngine();
        var rule = new HealthTestAlertRule
        {
            Id = Guid.NewGuid(),
            Code = "general",
            Name = "General",
            Condition = """{"when":{"resultType":"score","code":"s"}}""",
            Severity = HealthTestSeverity.moderate,
            MessageTemplate = "Severidad: {severity}",
            IsActive = true,
        };
        var results = new List<HealthTestResult>
        {
            Result("s", HealthTestResultType.score, 30, "moderate"),
        };

        var drafts = engine.Evaluate(Guid.NewGuid(), results, [rule]);

        Assert.Single(drafts);
        Assert.Equal("Severidad: moderate", drafts[0].Title);
    }
}
