using CoppAddresd.Application.DTOs.LabExam;

namespace CoppAddresd.Application.Features.LabExam;

/// <summary>
/// Comparación aritmética de valores, sin interpretar beneficio o deterioro clínico.
/// La dirección histórica se conserva neutral; Change expresa aumento, descenso o
/// igualdad exacta. Sin unidad/fecha comparable no se afirma una evolución temporal.
/// </summary>
public static class LabExamEvolutionBuilder
{
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
                    CurrentUnit: current.UnitSymbol,
                    Change: "first_record", CurrentDate: current.ObservedAt, CurrentSource: current.Source);
                continue;
            }

            var sameUnit = !string.IsNullOrWhiteSpace(previous.UnitSymbol) && !string.IsNullOrWhiteSpace(current.UnitSymbol)
                && string.Equals(previous.UnitSymbol.Trim(), current.UnitSymbol.Trim(), StringComparison.OrdinalIgnoreCase);
            var orderedDates = previous.ObservedAt is { } previousDate && current.ObservedAt is { } currentDate
                && previousDate <= currentDate;
            if (!sameUnit || !orderedDates)
            {
                result[current.MetricCode] = new MetricEvolution(
                    MetricDirection.Changed,
                    PreviousValue: previous.Value,
                    PreviousUnit: previous.UnitSymbol,
                    PreviousDate: previous.ObservedAt,
                    Delta: null,
                    CurrentValue: current.Value,
                    CurrentUnit: current.UnitSymbol,
                    Change: "not_comparable", CurrentDate: current.ObservedAt,
                    PreviousSource: previous.Source, CurrentSource: current.Source);
                continue;
            }

            var delta = current.Value - previous.Value;
            result[current.MetricCode] = new MetricEvolution(
                delta == 0m ? MetricDirection.Stable : MetricDirection.Changed,
                PreviousValue: previous.Value,
                PreviousUnit: previous.UnitSymbol,
                PreviousDate: previous.ObservedAt,
                Delta: delta,
                CurrentValue: current.Value,
                CurrentUnit: current.UnitSymbol,
                Change: delta > 0m ? "increase" : delta < 0m ? "decrease" : "unchanged",
                CurrentDate: current.ObservedAt, PreviousSource: previous.Source, CurrentSource: current.Source);
        }

        return result;
    }

}
