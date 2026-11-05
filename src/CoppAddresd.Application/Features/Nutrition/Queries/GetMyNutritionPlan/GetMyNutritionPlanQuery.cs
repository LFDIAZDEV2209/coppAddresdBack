using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Features.Wellness;
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
    ILogger<GetMyNutritionPlanQueryHandler> logger
) : IRequestHandler<GetMyNutritionPlanQuery, MyNutritionPlanDto?>
{
    public async Task<MyNutritionPlanDto?> Handle(
        GetMyNutritionPlanQuery request,
        CancellationToken ct
    )
    {
        // 1) Identidad: paciente del JWT vía patient_profiles.user_id.
        // Sin perfil → 404, nunca datos de otro paciente (anti-IDOR).
        var patient = await patients.GetByUserIdAsync(request.UserId, ct);
        if (patient is null)
        {
            throw new NotFoundException(
                "No existe un perfil de paciente para el usuario autenticado."
            );
        }

        // 2) Asignación activa: la asignación es la fuente de verdad del plan
        // asignado desde el ERP (el comando de asignación no toca la fila del
        // plan). Misma regla que ProgramContentResolver: Status Active, ventana
        // de fechas vigente (hoy local) y, en solapamiento, gana el inicio
        // más reciente y luego el identificador mayor.
        var zone = await wellness.GetPatientTimezoneAsync(patient.Id, ct);
        var now = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(zone))
        {
            try { now = TimeZoneInfo.ConvertTimeFromUtc(now, TimeZoneInfo.FindSystemTimeZoneById(zone)); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        var today = DateOnly.FromDateTime(now);
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
                        m.Notes,
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
}
