using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Features.Wellness;
using CoppAddresd.Application.Features.Nutrition.Engine;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Nutrition.Queries.GetMyNutritionPlan;

/// <summary>
/// Plan de alimentación activo del paciente autenticado (APP móvil,
/// self-service). El <c>UserId</c> llega resuelto de la identidad del JWT por
/// la capa API (nunca del body, anti-IDOR): se mapea a
/// <c>app.patient_profiles.user_id</c> y nunca se acepta un patientId del
/// cliente. Sin plan activo asignado devuelve <c>null</c> (el controlador
/// responde 404).
/// </summary>
public sealed record GetMyNutritionPlanQuery(Guid UserId) : IRequest<MyNutritionPlanDto?>;

public sealed class GetMyNutritionPlanQueryHandler(
    IPatientRepository patients,
    IWellnessRepository wellness,
    ILogger<GetMyNutritionPlanQueryHandler> logger,
    INutritionPrescriptionRepository prescriptions,
    TimeProvider clock
) : IRequestHandler<GetMyNutritionPlanQuery, MyNutritionPlanDto?>
{
    public async Task<MyNutritionPlanDto?> Handle(
        GetMyNutritionPlanQuery request,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        // 1) Identidad: paciente del JWT vía patient_profiles.user_id.
        // Sin perfil → 404, nunca datos de otro paciente (anti-IDOR).
        var patient = await patients.GetByUserIdAsync(request.UserId, ct);
        if (patient is null)
        {
            throw new NotFoundException(
                "No existe un perfil de paciente para el usuario autenticado."
            );
        }

        return await ReadForPatientAsync(patient, wellness, logger, prescriptions, clock, ct);
    }

    // Lectura compartida con el contexto interno: misma vigencia y mapeo, sin resolver identidad dos veces.
    internal static async Task<MyNutritionPlanDto?> ReadForPatientAsync(
        PatientProfile patient, IWellnessRepository wellness, ILogger logger,
        INutritionPrescriptionRepository prescriptions, TimeProvider clock, CancellationToken ct)
    {
        // 2) Día del paciente: primero publicación nueva; si no hay vigente,
        // se conserva el plan legacy aprobado con asignación Active y ventana
        // vigente. En cada modelo gana el inicio más reciente y luego el ID.
        var zone = patient.Timezone;
        var now = clock.GetUtcNow().UtcDateTime;
        if (!string.IsNullOrWhiteSpace(zone))
        {
            try { now = TimeZoneInfo.ConvertTimeFromUtc(now, TimeZoneInfo.FindSystemTimeZoneById(zone)); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        var today = DateOnly.FromDateTime(now);
        // La publicación vigente nueva tiene prioridad sobre cualquier asignación legacy.
        // Borradores y ciclos futuros/vencidos nunca ocultan una asignación vigente.
        var prescription = await prescriptions.GetCurrentPublishedAsync(patient.Id, today, ct);
        if (prescription is not null) return Compose(prescription, today, ct);
        var assignments = await wellness.ListPlanAssignmentsByPatientAsync(patient.Id, ct);
        var active = assignments
            .Where(a =>
                a.Status == AssignmentStatus.Active
                && DateOnly.FromDateTime(a.StartDate) <= today
                && (a.EndDate is null || DateOnly.FromDateTime(a.EndDate.Value) >= today)
            )
            .OrderByDescending(a => a.StartDate)
            .ThenByDescending(a => a.Id)
            .FirstOrDefault();

        if (active is null)
        {
            logger.LogDebug(
                "Me.NutritionPlan: paciente {PatientId} sin asignación activa",
                patient.Id
            );
            return null;
        }

        // 3) Plan con días ordenados (el repositorio ya ordena por DayNumber y
        // SortOrder; aquí solo se agrupa por día). Plan huérfano → null.
        var plan = await wellness.GetPlanByIdAsync(active.PlanId, ct);
        if (plan is null || !NutritionPlanReviewRules.IsPublished(plan)
            || plan.PatientId != patient.Id || today.DayNumber - DateOnly.FromDateTime(active.StartDate).DayNumber >= 30)
        {
            logger.LogWarning(
                "Me.NutritionPlan: asignación {AssignmentId} apunta a un plan inexistente {PlanId}",
                active.Id,
                active.PlanId
            );
            return null;
        }

        var days = plan
            .Days.GroupBy(d => d.DayNumber)
            .OrderBy(g => g.Key)
            .Select(g => new MyNutritionPlanDayDto(
                g.Key,
                g.OrderBy(m => m.SortOrder).First().DailyWaterMl,
                g.OrderBy(m => m.SortOrder)
                    .Select(m => new MyNutritionPlanMealDto(
                        m.MealType.ToString(),
                        m.Description,
                        m.Foods,
                        m.Calories,
                        m.ProteinG,
                        m.CarbsG,
                        m.FatG,
                        m.FiberG,
                        m.WaterMl,
                        PatientNutritionInstructions.ForPatient(m.Notes),
                        m.SortOrder
                    ))
                    .ToList()
            ))
            .ToList();

        logger.LogInformation(
            "Me.NutritionPlan: paciente {PatientId} plan {PlanId} días {Days}",
            patient.Id,
            plan.Id,
            days.Count
        );

        return new MyNutritionPlanDto(
            plan.Id,
            plan.Name,
            plan.TargetCondition,
            plan.DurationDays,
            plan.DailyCalorieTarget,
            plan.DailyProteinTarget,
            plan.DailyCarbsTarget,
            plan.DailyFatTarget,
            plan.DailyFiberTarget,
            days.Count > 0 ? days[0].DailyWaterMl : 2000,
            plan.Allergens,
            plan.MealTiming,
            plan.Status.ToString(),
            days,
            DateOnly.FromDateTime(active.StartDate),
            DateOnly.FromDateTime(active.StartDate).AddDays(29),
            today.DayNumber - DateOnly.FromDateTime(active.StartDate).DayNumber + 1,
            plan.Revision
        );
    }

    private static MyNutritionPlanDto Compose(NutritionPrescription plan, DateOnly today, CancellationToken ct)
    {
        var foods = plan.Meals.SelectMany(x => x.Items).Select(x => x.FoodItem!)
            .DistinctBy(x => x.Id).ToDictionary(x => x.Id);
        var engine = new NutritionEngine(foods);
        var verifiedEnglish = PatientNutritionInstructions.ForPatient(NutritionInstructionTranslation.VerifiedEnglish(plan));
        var days = plan.Meals.GroupBy(x => x.DayNumber).OrderBy(x => x.Key).Select(day =>
            new MyNutritionPlanDayDto(day.Key, plan.WaterMl ?? 2000,
                day.OrderBy(x => x.ScheduledTime).ThenBy(x => x.MealType, StringComparer.Ordinal).ThenBy(x => x.Id)
                    .Select((meal, order) =>
                    {
                        ct.ThrowIfCancellationRequested();
                        var items = meal.Items.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToArray();
                        var macros = engine.SumItems(items.Select(x => new NutritionItem(x.FoodItemId, x.Grams)));
                        // Snack es el valor histórico del contrato móvil para la merienda.
                        var type = meal.MealType switch { "des" => MealType.Desayuno, "alm" => MealType.Almuerzo,
                            "mer" => MealType.Snack, "cen" => MealType.Cena,
                            _ => throw new InvalidOperationException("Tipo de comida nutricional desconocido.") };
                        return new MyNutritionPlanMealDto(type.ToString(), meal.MenuName,
                            string.Join("; ", items.Select(x => $"{foods[x.FoodItemId].Name}: {engine.QtyText(new(x.FoodItemId, x.Grams))}")),
                            (int)macros.K, (decimal)macros.P, (decimal)macros.C, (decimal)macros.F,
                            (decimal)macros.Fi, meal.WaterMl, PatientNutritionInstructions.ForPatient(meal.Notes ?? plan.Instructions), order + 1,
                            meal.ScheduledTime.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                            // La traducción pertenece a la indicación general, nunca a una nota específica.
                            meal.Notes is null ? verifiedEnglish : null);
                    }).ToList())).ToList();
        return new(plan.Id, "Plan nutricional", null, 30, plan.CalorieTarget is { } k ? (int)NutritionEngine.R0(k) : null,
            (decimal?)plan.ProteinTarget, (decimal?)plan.CarbsTarget, (decimal?)plan.FatTarget, (decimal?)plan.FiberTarget,
            plan.WaterMl ?? 2000, null,
            string.Join(", ", plan.Meals.OrderBy(x => x.ScheduledTime).Select(x => x.ScheduledTime.ToString("HH:mm")).Distinct()),
            NutritionPlanStatus.Active.ToString(), days, plan.StartDate, plan.EndDate,
            today.DayNumber - plan.StartDate!.Value.DayNumber + 1, plan.Revision);
    }
}
