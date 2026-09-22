using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Patients;

/// <summary>
/// Siembra el histórico de mediciones clínicas (14 métricas × 12 meses) para un
/// paciente recién creado, con tendencia realista de mejora. Idempotente: no
/// duplica filas ya sembradas (source = <c>seed-hist</c>). DECISIÓN FASE 3
/// (Grill): todo paciente nuevo desde el ERP nace con histórico para poder
/// probar las pantallas de la APP; las filas quedan marcadas y trazables.
/// </summary>
public record SeedMeasurementHistoryCommand(Guid PatientId) : IRequest<int>;

public sealed class SeedMeasurementHistoryCommandHandler(
    IClinicalMeasurementRepository measurements,
    ILogger<SeedMeasurementHistoryCommandHandler> logger
) : IRequestHandler<SeedMeasurementHistoryCommand, int>
{
    private const string Source = "seed-hist";
    private const int Months = 12;

    // Trayectorias realistas de 12 meses (inicio → fin) por código de métrica.
    private static readonly (string Code, decimal Start, decimal End)[] Trajectories =
    [
        ("weight", 95m, 82m),
        ("bmi", 32.9m, 28.4m),
        ("body_fat", 38m, 29m),
        ("waist", 112m, 97m),
        ("hip", 112m, 103m),
        ("wrist", 17.5m, 17.0m),
        ("height", 1.70m, 1.70m),
        ("hba1c", 8.2m, 6.3m),
        ("glucose_fasting", 138m, 96m),
        ("systolic_bp", 138m, 119m),
        ("diastolic_bp", 88m, 76m),
        ("heart_rate", 78m, 67m),
        ("o2_saturation", 93m, 97m),
        ("temperature_c", 36.6m, 36.6m),
    ];

    public async Task<int> Handle(SeedMeasurementHistoryCommand request, CancellationToken ct)
    {
        var metrics = await measurements.GetActiveMetricsWithUnitsAsync(ct);
        var metricByCode = metrics
            .Where(m => m.IsActive)
            .GroupBy(m => m.Code)
            .Select(g => g.First())
            .ToDictionary(m => m.Code);

        var existing = await measurements.ListByPatientAsync(request.PatientId, ct);
        var existingKeys = existing
            .Select(x => $"seed-hist:{request.PatientId:N}:{x.Metric}:{x.ObservedAt:yyyy-MM}")
            .ToHashSet();

        var noise = new Random(request.PatientId.GetHashCode());
        var batch = new List<ClinicalMeasurement>(Trajectories.Length * Months);
        var today = DateTime.UtcNow;

        foreach (var (code, start, end) in Trajectories)
        {
            if (!metricByCode.TryGetValue(code, out var metric))
                continue;

            for (var i = 0; i < Months; i++)
            {
                var observed = new DateTime(
                    today.Year,
                    today.Month,
                    15,
                    10,
                    0,
                    0,
                    DateTimeKind.Utc
                ).AddMonths(-(Months - 1 - i));

                var sourceKey = $"seed-hist:{request.PatientId:N}:{code}:{observed:yyyy-MM}";
                if (existingKeys.Contains(sourceKey))
                    continue;

                var progress = Months == 1 ? 0m : (decimal)i / (Months - 1);
                var noiseFactor = 1m + (decimal)(noise.NextDouble() * 0.02 - 0.01);
                var value = Math.Round((start - (start - end) * progress) * noiseFactor, 1);
                if (value < 0)
                    value = 0m;

                batch.Add(
                    new ClinicalMeasurement
                    {
                        PatientId = request.PatientId,
                        MetricId = metric.Id,
                        Value = value,
                        UnitId = metric.DefaultUnitId,
                        ObservedAt = observed,
                        RecordedAt = DateTime.UtcNow,
                        Source = Source,
                        SourceKey = sourceKey,
                    }
                );
            }
        }

        if (batch.Count == 0)
            return 0;

        await measurements.AddBatchAsync(batch, ct);
        logger.LogInformation(
            "Histórico de mediciones sembrado para paciente {PatientId}: {Count} filas",
            request.PatientId,
            batch.Count
        );
        return batch.Count;
    }
}
