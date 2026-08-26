using CoppAddresd.Application.DTOs.Ai;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Services;

/// <summary>
/// Consolida el contexto clínico de un paciente para la generación de planes
/// con IA. Regla de consolidación de mediciones: si existe una
/// <see cref="Domain.Entities.ClinicalMeasurement"/> para la métrica, gana;
/// si no, se hace fallback a <see cref="Domain.Entities.VitalSign"/> para las
/// métricas equivalentes (peso, talla, presión sistólica/diastólica y
/// frecuencia cardíaca). Nunca se devuelve la misma métrica duplicada.
/// </summary>
public sealed class ClinicalContextService(
    IPatientRepository patients,
    IClinicalMeasurementRepository measurements,
    ILogger<ClinicalContextService> logger) : IClinicalContextService
{
    public async Task<ClinicalContextDto> ConsolidateAsync(Guid patientId, CancellationToken ct = default)
    {
        var patient = await patients.GetByIdAsync(patientId, ct)
            ?? throw new NotFoundException($"Paciente {patientId} no encontrado");

        // Mediciones clínicas consolidadas: el repositorio devuelve la serie
        // ordenada por observación descendente; la primera por métrica gana.
        var clinical = await measurements.ListByPatientAsync(patientId, ct);
        var byMetric = new Dictionary<string, ClinicalMeasurementDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var measurement in clinical)
            byMetric.TryAdd(measurement.Metric, measurement);

        // Fallback a la última toma de VitalSign para métricas sin medición
        // clínica registrada (modelo legacy con columnas duras).
        var latestVital = patient.VitalSigns
            .OrderByDescending(v => v.MeasuredAt)
            .FirstOrDefault();

        if (latestVital is not null)
        {
            AddFallback(byMetric, "weight", latestVital.WeightKg, "kg", latestVital.MeasuredAt);
            AddFallback(byMetric, "height", latestVital.HeightCm, "cm", latestVital.MeasuredAt);
            AddFallback(byMetric, "systolic_bp", latestVital.Systolic, "mmhg", latestVital.MeasuredAt);
            AddFallback(byMetric, "diastolic_bp", latestVital.Diastolic, "mmhg", latestVital.MeasuredAt);
            AddFallback(byMetric, "heart_rate", latestVital.HeartRate, "bpm", latestVital.MeasuredAt);
        }

        var age = patient.DateOfBirth is null ? 0 : CalculateAge(patient.DateOfBirth.Value);

        var context = new ClinicalContextDto(
            new ClinicalPatientDto(age, patient.Gender, patient.ExerciseLevel),
            byMetric.Values
                .OrderBy(m => m.Metric, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            patient.Allergies
                .Select(a => a.Allergen!.Name)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            patient.Diagnoses
                .Select(d => $"{d.Icd10Code!.Code}: {d.Icd10Code.Description}")
                .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            patient.Medications
                .Select(m => m.Frequency is null
                    ? m.Medication!.Name
                    : $"{m.Medication!.Name} ({m.Frequency})")
                .OrderBy(med => med, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            new ClinicalLifestyleDto(
                patient.SmokingStatus,
                patient.AlcoholStatus,
                patient.ExerciseLevel));

        logger.LogInformation(
            "Contexto clínico consolidado para paciente {PatientId}: {MeasurementCount} mediciones, {AllergyCount} alergias, {DiagnosisCount} diagnósticos, {MedicationCount} medicamentos",
            patientId, context.Measurements.Count, context.Allergies.Count,
            context.Diagnoses.Count, context.Medications.Count);

        return context;
    }

    /// <summary>
    /// Agrega una métrica de respaldo si no existe una medición clínica para
    /// ella y el valor legacy no es nulo.
    /// </summary>
    private static void AddFallback(
        Dictionary<string, ClinicalMeasurementDto> byMetric,
        string metric,
        decimal? value,
        string unit,
        DateTime observedAt)
    {
        if (value is null || byMetric.ContainsKey(metric))
            return;

        byMetric[metric] = new ClinicalMeasurementDto(metric, value.Value, unit, observedAt);
    }

    private static int CalculateAge(DateTime dateOfBirth)
    {
        var today = DateTime.UtcNow.Date;
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth.Date > today.AddYears(-age))
            age--;
        return Math.Max(age, 0);
    }
}