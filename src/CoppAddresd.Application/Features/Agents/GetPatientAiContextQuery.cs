using System.Text.Json;
using CoppAddresd.Application.DTOs.LabExam;
using CoppAddresd.Application.Features.Measurements.Queries.GetMyMeasurements;
using CoppAddresd.Application.Features.Nutrition;
using CoppAddresd.Application.Features.Nutrition.Queries.GetMyNutritionPlan;
using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Agents;

/// <summary>Contexto mínimo para agentes; datos ausentes se conservan como null o listas vacías.</summary>
public sealed record PatientAiContextDto(Guid PatientId,
    IReadOnlyList<LabExamMetricSnapshot> Measurements, PatientAiNutritionDto? NutritionPlan,
    IReadOnlyList<PatientAiDeviceMetricDto> DeviceMetrics, PatientAiDiagnosisDto? PrimaryDiagnosis);
public sealed record PatientAiNutritionDto(string Name, int? Kcal, decimal? ProteinG,
    decimal? CarbsG, decimal? FiberG, IReadOnlyList<PatientAiMealDto> Meals,
    string Disclaimer = NutritionRecommendationNotice.Text, DateOnly? StartDate = null, DateOnly? EndDate = null, int? CurrentDay = null);
public sealed record PatientAiMealDto(string Type, string? Description, int? Kcal);
public sealed record PatientAiDeviceMetricDto(string Code, DateOnly Date, decimal Value, string Unit);
public sealed record PatientAiDiagnosisDto(string Code, string? Description);
public sealed record GetPatientAiContextQuery(Guid UserId) : IRequest<PatientAiContextDto?>;

public sealed class GetPatientAiContextQueryHandler(
    IPatientRepository patients, IClinicalMeasurementRepository measurements,
    IPatientMeasurementRepository deviceMetrics, IWellnessRepository wellness,
    INutritionPrescriptionRepository prescriptions, TimeProvider clock,
    ILogger<GetPatientAiContextQueryHandler> logger)
    : IRequestHandler<GetPatientAiContextQuery, PatientAiContextDto?>
{
    private static readonly string[] MetricCodes = ["weight", "height", "bmi", "body_fat",
        "waist_circumference", "systolic_bp", "diastolic_bp", "heart_rate", "glucose_fasting",
        "hba1c", "cholesterol_total", "hdl", "ldl", "triglycerides"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PatientAiContextDto?> Handle(GetPatientAiContextQuery request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var patient = await ReadAsync(() => patients.GetByUserIdAsync(request.UserId, ct), null, "paciente", ct);
        if (patient is null || patient.DeletedAt is not null || patient.Status != "Activo") return null;

        // Secuencial: los repositorios comparten el DbContext scoped. No hay consultas por fila.
        var latest = await ReadAsync(() => measurements.GetLastPerMetricAsync(
            patient.Id, MetricCodes, Guid.Empty, ct),
            (IReadOnlyDictionary<string, LabExamMetricSnapshot>)new Dictionary<string, LabExamMetricSnapshot>(), "mediciones", ct);
        var plan = await ReadAsync(() => GetMyNutritionPlanQueryHandler.ReadForPatientAsync(
            patient, wellness, logger, prescriptions, clock, ct), null, "plan nutricional", ct);
        var end = clock.GetUtcNow().UtcDateTime;
        var rows = await ReadAsync(() => deviceMetrics.GetDeviceWindowAsync(patient.Id, end.AddDays(-7), end, ct),
            (IReadOnlyList<MeasurementItemDto>)[], "dispositivo", ct);
        var diagnosis = await ReadAsync(() => patients.GetPrimaryDiagnosisAsync(patient.Id, ct), null, "diagnóstico", ct);

        // Solo las comidas del día vigente; no se envía el calendario completo de treinta días.
        var menuDay = plan is { Days.Count: > 0, CurrentDay: > 0 }
            ? (plan.CurrentDay.Value - 1) % plan.Days.Count + 1 : (int?)null;
        var meals = plan?.Days.FirstOrDefault(d => d.DayNumber == menuDay)?.Meals
            .Take(6).Select(m => new PatientAiMealDto(Clip(m.MealType, 24)!, Clip(m.Description, 100), m.Calories)).ToList() ?? [];
        var nutrition = plan is null ? null : new PatientAiNutritionDto(Clip(plan.Name, 80)!,
            plan.DailyCalorieTarget, plan.DailyProteinTarget, plan.DailyCarbsTarget, plan.DailyFiberTarget, meals,
            plan.Disclaimer, plan.StartDate, plan.EndDate, plan.CurrentDay);
        var devices = rows.GroupBy(m => (m.MetricCode, DateOnly.FromDateTime(m.ObservedAt.UtcDateTime)))
            .Select(g => g.OrderByDescending(m => m.ObservedAt).ThenByDescending(m => m.Id).First())
            .OrderBy(m => m.ObservedAt).ThenBy(m => m.MetricCode, StringComparer.Ordinal)
            .Select(m => new PatientAiDeviceMetricDto(m.MetricCode,
                DateOnly.FromDateTime(m.ObservedAt.UtcDateTime), m.Value, m.UnitCode)).ToList();
        var result = new PatientAiContextDto(patient.Id, latest.Values.OrderBy(m => m.MetricCode).ToList(),
            nutrition, devices, diagnosis is null ? null : new(Clip(diagnosis.Icd10Code, 20)!, Clip(diagnosis.Description, 160)));

        // Presupuesto real en UTF-8, incluido el escape JSON. Se recortan primero los resúmenes.
        while (JsonSerializer.SerializeToUtf8Bytes(result, JsonOptions).Length >= 8000)
        {
            if (meals.Count > 0) meals.RemoveAt(meals.Count - 1);
            else if (devices.Count > 0) devices.RemoveAt(0);
            else if (result.Measurements.Count > 0) result = result with { Measurements = result.Measurements.SkipLast(1).ToList() };
            else break;
        }
        return result;
    }

    private async Task<T> ReadAsync<T>(Func<Task<T>> read, T fallback, string block, CancellationToken ct)
    {
        try { return await read(); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            // No registrar valores clínicos, identidad ni secretos al degradar un bloque.
            logger.LogWarning("Contexto IA: bloque {Block} no disponible ({ErrorType})", block, ex.GetType().Name);
            return fallback;
        }
    }

    private static string? Clip(string? value, int max) => value is { Length: > 0 } && value.Length > max ? value[..max] : value;
}
