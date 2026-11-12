using System.Globalization;
using System.Text.Json;
using CoppAddresd.Application.DTOs.LabExam;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Features.Sos;

/// <summary>Congela lecturas reales de hasta 24 horas al activar la alerta; nunca se refrescan durante los reintentos de envío.</summary>
public static class SosVitalsSnapshot
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string[] PulseCodes = ["HR", "HEART_RATE", "PULSE"];
    private static readonly string[] OxygenCodes = ["SPO2", "O2_SATURATION", "OXYGEN_SATURATION"];
    private static readonly string[] SystolicCodes = ["SBP", "SYSTOLIC_BP", "BLOOD_PRESSURE_SYSTOLIC", "BP_SYS"];
    private static readonly string[] DiastolicCodes = ["DBP", "DIASTOLIC_BP", "BLOOD_PRESSURE_DIASTOLIC", "BP_DIA"];

    public static string Serialize(IReadOnlyList<SosVitalReading> readings) => JsonSerializer.Serialize(readings, Json);

    public static IReadOnlyList<SosVitalReading> Read(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        try { return JsonSerializer.Deserialize<SosVitalReading[]>(value, Json) ?? []; }
        catch (JsonException) { return []; }
    }

    public static async Task<IReadOnlyList<SosVitalReading>> ResolveAsync(Guid patientId, SosVitalsDto? input,
        IClinicalMeasurementRepository? measurements, DateTime now, CancellationToken ct)
    {
        var aliases = PulseCodes.Concat(OxygenCodes).Concat(SystolicCodes).Concat(DiastolicCodes);
        var stored = measurements is null ? new Dictionary<string, LabExamMetricSnapshot>()
            : await measurements.GetLastPerMetricAsync(patientId, aliases, Guid.Empty, ct);
        var result = new List<SosVitalReading>();
        bool ValidTime(DateTime? time) => time.HasValue && time <= now && time >= now.AddHours(-24);
        static bool RealSource(string? source) => source is "device" or "patient" or "professional" or "lab";
        void Add(string code, string[] names, string unit, decimal? reported, DateTime? observed, decimal min, decimal max)
        {
            var row = stored.Values.Where(x => names.Contains(x.MetricCode.ToUpperInvariant()) && RealSource(x.Source) && ValidTime(x.ObservedAt)
                && x.Value >= min && x.Value <= max && (string.Equals(x.UnitSymbol, unit, StringComparison.OrdinalIgnoreCase)
                    || code == "HR" && string.Equals(x.UnitSymbol, "bpm", StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(x => x.ObservedAt).FirstOrDefault();
            // Clientes anteriores sin fecha siguen siendo auto-reporte, nunca una medición "en vivo".
            if (reported.HasValue && reported >= min && reported <= max
                && ((ValidTime(observed) && (row is null || observed >= row.ObservedAt)) || (observed is null && row is null)))
                result.Add(new(code, reported.Value, unit, observed, "app"));
            else if (row is not null)
                result.Add(new(code, row.Value, unit, row.ObservedAt, row.Source!));
        }
        Add("HR", PulseCodes, "lpm", input?.HeartRate, input?.HeartRateObservedAt, 20, 300);
        Add("SPO2", OxygenCodes, "%", input?.Spo2, input?.Spo2ObservedAt, 50, 100);
        var pressure = input?.BloodPressure?.Split('/');
        decimal? sys = pressure?.Length == 2 && decimal.TryParse(pressure[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var systolic) ? systolic : null;
        decimal? dia = pressure?.Length == 2 && decimal.TryParse(pressure[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var diastolic) ? diastolic : null;
        Add("SBP", SystolicCodes, "mmHg", sys, input?.BloodPressureObservedAt, 1, 350);
        Add("DBP", DiastolicCodes, "mmHg", dia, input?.BloodPressureObservedAt, 1, 250);
        var a = result.Find(x => x.MetricCode == "SBP");
        var b = result.Find(x => x.MetricCode == "DBP");
        // Una presión es un par de la misma toma, no dos medidas inconexas.
        if (a is null || b is null || a.ObservedAt != b.ObservedAt || a.Source != b.Source)
            result.RemoveAll(x => x.MetricCode is "SBP" or "DBP");
        return result;
    }
}
