using CoppAddresd.Application.DTOs.Ai;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Services;

/// <summary>
/// Evalúa las reglas de seguridad clínicas activas contra las mediciones
/// consolidadas del paciente. Las reglas se cargan en una única query (son
/// pocas) y se comparan en la misma unidad de la métrica (<see cref="PlanSafetyRule.UnitCode"/>);
/// si la unidad de la medición no coincide con la de la regla, la regla se
/// omite (no se puede comparar valores de unidades distintas).
/// </summary>
public sealed class SafetyRulesService(
    ISafetyRuleRepository rules,
    ILogger<SafetyRulesService> logger) : ISafetyRulesService
{
    public async Task<IReadOnlyList<RestrictionDto>> EvaluateAsync(
        ClinicalContextDto context,
        CancellationToken ct = default)
    {
        var activeRules = await rules.GetActiveAsync(ct);

        if (activeRules.Count == 0)
        {
            logger.LogInformation("Sin reglas de seguridad activas para evaluar");
            return [];
        }

        var restrictions = new List<RestrictionDto>();

        foreach (var rule in activeRules)
        {
            var measurement = context.Measurements
                .FirstOrDefault(m => string.Equals(
                    m.Metric, rule.MetricCode, StringComparison.OrdinalIgnoreCase));

            if (measurement is null)
                continue;

            if (!string.Equals(measurement.Unit, rule.UnitCode, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(
                    "Regla {MetricCode} omitida: la medición usa unidad {MeasurementUnit} y la regla exige {RuleUnit}",
                    rule.MetricCode, measurement.Unit, rule.UnitCode);
                continue;
            }

            if (!IsViolated(rule, measurement.Value))
                continue;

            var severity = rule.Severity == SafetySeverity.Block ? "block" : "warning";
            restrictions.Add(new RestrictionDto(rule.Restriction, severity));

            logger.LogInformation(
                "Restricción aplicada: {MetricCode} {Operator} {ThresholdMin} → {Severity}",
                rule.MetricCode, rule.Operator, rule.ThresholdMin, severity);
        }

        return restrictions;
    }

    /// <summary>
    /// Evalúa el operador de la regla contra el valor de la medición. Los
    /// operadores unarios usan <see cref="PlanSafetyRule.ThresholdMin"/>;
    /// <c>between</c> usa ambos umbrales.
    /// </summary>
    private static bool IsViolated(PlanSafetyRule rule, decimal value) => rule.Operator switch
    {
        ">" => value > rule.ThresholdMin,
        ">=" => value >= rule.ThresholdMin,
        "<" => value < rule.ThresholdMin,
        "<=" => value <= rule.ThresholdMin,
        "between" => value >= rule.ThresholdMin && value <= rule.ThresholdMax,
        // Operador desconocido: se omite la regla (no bloquea la generación).
        _ => false,
    };
}