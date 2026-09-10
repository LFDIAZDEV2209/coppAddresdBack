using CoppAddresd.Application.DTOs.LabExam;

namespace CoppAddresd.Application.Features.LabExam;

/// <summary>
/// Cálculo puro de la evolución por métrica (R2/R3): el backend es dueño de la
/// aritmética (delta) y de la semántica de dirección; el LLM solo narra.
/// Regla: |Δ| ≤ eps ⇒ <see cref="MetricDirection.Stable"/>; Δ ≥ W ⇒
/// <see cref="MetricDirection.Worsened"/>; Δ ≤ −W ⇒
/// <see cref="MetricDirection.Improved"/>; en la zona neutral ⇒
/// <see cref="MetricDirection.Changed"/>. Solo las métricas con regla clínica
/// defendible (BP, glucosa en ayunas, hba1c) pueden ser Worsened/Improved.
/// Un cambio de unidad nunca se resta: Changed con delta null.
/// </summary>
public static class LabExamEvolutionBuilder
{
    private sealed record Threshold(decimal Epsilon, decimal? WorsenedAt, decimal? ImprovedAt);

    /// <summary>Tabla de umbrales de 14 métricas aprobada por producto (design Q2).</summary>
    private static readonly Dictionary<string, Threshold> Thresholds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["systolic_bp"] = new(5m, 10m, -10m),
        ["diastolic_bp"] = new(5m, 10m, -10m),
        ["glucose_fasting"] = new(5m, 15m, -15m),
        ["hba1c"] = new(0.2m, 0.5m, -0.5m),
        ["o2_saturation"] = new(2m, null, null),
        ["heart_rate"] = new(5m, null, null),
        ["temperature_c"] = new(0.5m, null, null),
        ["weight"] = new(0.5m, null, null),
        ["height"] = new(0.5m, null, null),
        ["bmi"] = new(0.3m, null, null),
        ["body_fat"] = new(0.5m, null, null),
        ["waist"] = new(0.5m, null, null),
        ["hip"] = new(0.5m, null, null),
        ["wrist"] = new(0.5m, null, null),
    };

    /// <summary>
    /// Construye la evolución de cada métrica actual contra su última medición previa
    /// (cualquier origen, excluyendo el lote actual). Devuelve un diccionario por código
    /// canónico; las métricas actuales duplicadas resuelven con la primera ocurrencia.
    /// </summary>
    public static IReadOnlyDictionary<string, MetricEvolution> Build(
        IReadOnlyList<LabExamMetricSnapshot> currentMetrics,
        IReadOnlyDictionary<string, LabExamMetricSnapshot> previousMetrics)
    {
        var result = new Dictionary<string, MetricEvolution>(StringComparer.OrdinalIgnoreCase);

        foreach (var current in currentMetrics)
        {
            if (string.IsNullOrWhiteSpace(current.MetricCode) || result.ContainsKey(current.MetricCode))
            {
                continue;
            }

            if (!previousMetrics.TryGetValue(current.MetricCode, out var previous))
            {
                result[current.MetricCode] = new MetricEvolution(
                    MetricDirection.FirstRecord,
                    CurrentValue: current.Value,
                    CurrentUnit: current.UnitSymbol);
                continue;
            }

            var sameUnit = string.Equals(previous.UnitSymbol, current.UnitSymbol, StringComparison.OrdinalIgnoreCase);
            if (!sameUnit)
            {
                result[current.MetricCode] = new MetricEvolution(
                    MetricDirection.Changed,
                    PreviousValue: previous.Value,
                    PreviousUnit: previous.UnitSymbol,
                    PreviousDate: previous.ObservedAt,
                    Delta: null,
                    CurrentValue: current.Value,
                    CurrentUnit: current.UnitSymbol);
                continue;
            }

            var delta = current.Value - previous.Value;
            result[current.MetricCode] = new MetricEvolution(
                Classify(current.MetricCode, delta),
                PreviousValue: previous.Value,
                PreviousUnit: previous.UnitSymbol,
                PreviousDate: previous.ObservedAt,
                Delta: delta,
                CurrentValue: current.Value,
                CurrentUnit: current.UnitSymbol);
        }

        return result;
    }

    private static MetricDirection Classify(string metricCode, decimal delta)
    {
        if (!Thresholds.TryGetValue(metricCode, out var threshold))
        {
            // Métrica fuera del catálogo: sin regla clínica ⇒ dirección neutral.
            return delta == 0m ? MetricDirection.Stable : MetricDirection.Changed;
        }

        if (Math.Abs(delta) <= threshold.Epsilon)
        {
            return MetricDirection.Stable;
        }

        if (threshold.WorsenedAt is { } worsenedAt && delta >= worsenedAt)
        {
            return MetricDirection.Worsened;
        }

        if (threshold.ImprovedAt is { } improvedAt && delta <= improvedAt)
        {
            return MetricDirection.Improved;
        }

        return MetricDirection.Changed;
    }
}
