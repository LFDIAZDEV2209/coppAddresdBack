using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Services.ProgramProgress;

/// <summary>
/// Resolvedor de contenido del programa para un paciente en una fecha local
/// (SPEC §4.2/§4.3/§6.10). Para un par (patientId, localDate):
/// <list type="bullet">
///   <item>Devuelve el plan de alimentación activo con su día correspondiente al weekday.</item>
///   <item>Devuelve la rutina de ejercicio activa (o <c>null</c> si no hay).</item>
/// </list>
///
/// Algoritmo de selección (SPEC §6.10): de todas las asignaciones con status
/// Active y ventana de fechas que cubre localDate, gana la de start_date más
/// reciente (overlap rule).
///
/// Servicio de solo lectura; consume <see cref="IWellnessRepository"/> (MVP:
/// lectura completa por paciente + filtrado en memoria, aceptable para single
/// patient). Evita N+1 cargando los días del plan una sola vez por resolved plan
/// (skill n-plus-one).
/// </summary>
public sealed class ProgramContentResolver(
    IWellnessRepository wellnessRepository,
    ILogger<ProgramContentResolver> logger) : IProgramContentResolver
{
    public async Task<ProgramContentResolution?> ResolveAsync(
        Guid patientId,
        DateOnly localDate,
        CancellationToken ct = default)
    {
        // --- 1. Cargar asignaciones del paciente (2 queries batch) ---
        var planAssignments = await wellnessRepository
            .ListPlanAssignmentsByPatientAsync(patientId, ct);
        var routineAssignments = await wellnessRepository
            .ListAssignmentsByPatientAsync(patientId, ct);

        // --- 2. Resolver plan de alimentación activo ---
        var activePlanAssignment = planAssignments
            .Where(a => a.Status == AssignmentStatus.Active
                        && IsDateInWindow(a.StartDate, a.EndDate, localDate))
            .OrderByDescending(a => a.StartDate)
            .FirstOrDefault();

        Guid? nutritionPlanId = null;
        int? resolvedDayNumber = null;

        if (activePlanAssignment is not null)
        {
            nutritionPlanId = activePlanAssignment.PlanId;

            // Cargar días del plan una sola vez (anti N+1: batch by plan, skill n-plus-one).
            // ListPlanDaysAsync ya existe en IWellnessRepository y trae todos los
            // NutritionPlanDay del plan.
            var planDays = await wellnessRepository
                .ListPlanDaysAsync(activePlanAssignment.PlanId, ct);

            // Mapear weekday local (ISO 8601: 1=Lun..7=Dom) al DayNumber del plan
            var weekday = ToIsoWeekday(localDate);
            var matchingDay = planDays.FirstOrDefault(d => d.DayNumber == weekday);
            resolvedDayNumber = matchingDay?.DayNumber;
        }

        // --- 3. Resolver rutina de ejercicio activa ---
        var activeRoutineAssignment = routineAssignments
            .Where(a => a.Status == AssignmentStatus.Active
                        && IsDateInWindow(a.StartDate, a.EndDate, localDate))
            .OrderByDescending(a => a.StartDate)
            .FirstOrDefault();

        // --- 4. Si no hay contenido activo, devolver null ---
        if (activePlanAssignment is null && activeRoutineAssignment is null)
        {
            logger.LogDebug(
                "Program.ContentResolver: patient={PatientId} date={LocalDate} → sin contenido activo",
                patientId, localDate);
            return null;
        }

        var resolution = new ProgramContentResolution
        {
            NutritionPlanId = nutritionPlanId,
            NutritionPlanDayNumber = resolvedDayNumber,
            ExerciseRoutineId = activeRoutineAssignment?.RoutineId,
        };

        logger.LogInformation(
            "Program.ContentResolver: patient={PatientId} date={LocalDate} " +
            "nutritionPlan={NutritionPlanId} day={DayNumber} routine={RoutineId}",
            patientId,
            localDate,
            resolution.NutritionPlanId?.ToString() ?? "null",
            resolution.NutritionPlanDayNumber?.ToString() ?? "null",
            resolution.ExerciseRoutineId?.ToString() ?? "null");

        return resolution;
    }

    /// <summary>
    /// Verifica si la fecha local cae dentro de la ventana de la asignación.
    /// Regla SPEC §6.10: start_date &lt;= localDate AND localDate &lt;= COALESCE(end_date, 'infinity').
    /// Compara solo la porción de fecha (DateTime → DateOnly).
    /// </summary>
    private static bool IsDateInWindow(DateTime startDate, DateTime? endDate, DateOnly localDate)
    {
        var start = DateOnly.FromDateTime(startDate);
        if (localDate < start)
            return false;

        // Sin end_date = ventana abierta (equivale a 'infinity')
        if (endDate is not { } end)
            return true;

        return localDate <= DateOnly.FromDateTime(end);
    }

    /// <summary>
    /// Convierte <see cref="DayOfWeek"/> de .NET (Sunday=0) a ISO 8601 weekday
    /// (Monday=1, Sunday=7), alineado con la convención de
    /// <c>NutritionPlanDay.DayNumber</c>.
    /// </summary>
    private static int ToIsoWeekday(DateOnly date)
    {
        // DayOfWeek: Sunday=0, Monday=1, ..., Saturday=6
        // ISO 8601: Monday=1, ..., Sunday=7
        return date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
    }
}
