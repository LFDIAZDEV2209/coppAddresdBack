using System.Text.Json;
using System.Text.Json.Serialization;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Application.Features.HealthTests.Scoring;

/// <summary>Condición de una regla de alerta (jsonb <c>condition</c> de HealthTestAlertRule).</summary>
public sealed record AlertCondition
{
    [JsonPropertyName("when")]
    public AlertWhen When { get; init; } = new();
}

/// <summary>Qué dispara la alerta: un tipo de resultado, su código y las severidades.</summary>
public sealed record AlertWhen
{
    [JsonPropertyName("resultType")]
    public string ResultType { get; init; } = "score";

    [JsonPropertyName("code")]
    public string Code { get; init; } = default!;

    [JsonPropertyName("severity")]
    public IReadOnlyList<string> Severity { get; init; } = [];
}

/// <summary>Draft de alerta producido por el motor antes de persistir.</summary>
public sealed record AlertDraft(
    Guid PatientId,
    Guid ResultId,
    Guid RuleId,
    HealthTestSeverity Severity,
    string Title,
    string Body
);

/// <summary>
/// Motor de alertas clínicas (SPEC A12). Evalúa las reglas activas contra los
/// resultados de una evaluación y produce drafts de alerta. La deduplicación
/// (una regla → a lo sumo una alerta por resultado) la aplica el caller
/// consultando el repositorio antes de insertar.
/// </summary>
public sealed class AlertEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public AlertCondition Parse(string conditionJson)
    {
        var condition = JsonSerializer.Deserialize<AlertCondition>(conditionJson, JsonOptions);
        return condition ?? new AlertCondition();
    }

    public bool Matches(AlertCondition condition, HealthTestResult result)
    {
        var when = condition.When;
        if (result.ResultType.ToString() != when.ResultType)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(when.Code) && result.Code != when.Code)
        {
            return false;
        }

        if (when.Severity.Count > 0 && result.Severity.HasValue)
        {
            return when.Severity.Contains(result.Severity.Value.ToString());
        }

        return true;
    }

    /// <summary>
    /// Evalúa las reglas activas contra los resultados de la evaluación y
    /// produce drafts (aún no persistidos). El título se construye desde la
    /// plantilla de la regla con placeholders <c>{code}</c>/<c>{value}</c>/
    /// <c>{label}</c>/<c>{severity}</c>.
    /// </summary>
    public IReadOnlyList<AlertDraft> Evaluate(
        Guid patientId,
        IReadOnlyList<HealthTestResult> results,
        IReadOnlyList<HealthTestAlertRule> rules
    )
    {
        var drafts = new List<AlertDraft>();
        foreach (var rule in rules)
        {
            var condition = Parse(rule.Condition);
            foreach (var result in results)
            {
                if (!Matches(condition, result))
                {
                    continue;
                }

                var severity = rule.Severity;
                var template = rule.MessageTemplate ?? $"Alerta: {result.Label} ({result.Value})";
                var title = template
                    .Replace("{code}", result.Code)
                    .Replace("{value}", result.Value.ToString("0.##"))
                    .Replace("{label}", result.Label)
                    .Replace("{severity}", result.Severity?.ToString() ?? severity.ToString());

                drafts.Add(
                    new AlertDraft(patientId, result.Id, rule.Id, severity, title, template)
                );
            }
        }

        return drafts;
    }
}
