using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.GetProgramContent;

/// <summary>
/// Handler de <see cref="GetProgramContentQuery"/> (T-77): resuelve el contenido
/// de nutrición y ejercicio configurado por semana para una inscripción.
///
/// Para cada semana calcula la ventana [start, end] y usa el resolvedor de
/// contenido para determinar el plan/rutina activo que cubre la ventana.
/// Devuelve null si la inscripción no existe.
/// </summary>
public sealed class GetProgramContentHandler(
    IProgramRepository programRepository,
    IProgramContentResolver contentResolver) : IRequestHandler<GetProgramContentQuery, ProgramContentResponse?>
{
    public async Task<ProgramContentResponse?> Handle(
        GetProgramContentQuery request, CancellationToken ct)
    {
        var enrollment = await programRepository.GetEnrollmentAsync(request.EnrollmentId, ct);
        if (enrollment is null)
        {
            return null;
        }

        // T-83 (B18): una sola carga de asignaciones para todas las semanas
        // (ResolveRangeAsync) + resolución de nombres por ID DISTINTO (batch) —
        // antes se llamaba al resolvedor por semana (hasta 83 cargas).
        var lastWeekStart = enrollment.StartLocalDate.AddDays((enrollment.TotalWeeks - 1) * 7);
        var resolutions = await contentResolver.ResolveRangeAsync(
            enrollment.PatientId, enrollment.StartLocalDate, lastWeekStart, ct);

        // Nombres de planes/rutinas: una query por ID DISTINTO (no por semana).
        var planNames = new Dictionary<Guid, (string Code, string Name)>();
        foreach (var planId in resolutions.Values
                     .Select(r => r.NutritionPlanId)
                     .Where(id => id is not null)
                     .Cast<Guid>()
                     .Distinct())
        {
            if (await programRepository.GetPlanNameAsync(planId, ct) is { } planName)
            {
                planNames[planId] = planName;
            }
        }

        var routineNames = new Dictionary<Guid, (string Code, string Name)>();
        foreach (var routineId in resolutions.Values
                     .Select(r => r.ExerciseRoutineId)
                     .Where(id => id is not null)
                     .Cast<Guid>()
                     .Distinct())
        {
            if (await programRepository.GetRoutineNameAsync(routineId, ct) is { } routineName)
            {
                routineNames[routineId] = routineName;
            }
        }

        var weeks = new List<ProgramContentWeekDto>(enrollment.TotalWeeks);

        for (var weekNumber = 1; weekNumber <= enrollment.TotalWeeks; weekNumber++)
        {
            var weekStart = enrollment.StartLocalDate.AddDays((weekNumber - 1) * 7);
            var weekEnd = weekStart.AddDays(6);

            // Resolver contenido al inicio de la ventana (SPEC §4.2/§4.3):
            // la asignación activa que cubre el start de la semana gana.
            resolutions.TryGetValue(weekStart, out var resolution);

            ProgramContentPlanRef? planRef = null;
            if (resolution?.NutritionPlanId is { } planId)
            {
                var planName = planNames.GetValueOrDefault(planId);
                planRef = new ProgramContentPlanRef(planId, planName.Name ?? "unknown", planName.Name ?? "unknown");
            }

            ProgramContentRoutineRef? routineRef = null;
            if (resolution?.ExerciseRoutineId is { } routineId)
            {
                var routineName = routineNames.GetValueOrDefault(routineId);
                routineRef = new ProgramContentRoutineRef(routineId, routineName.Name ?? "unknown", routineName.Name ?? "unknown");
            }

            weeks.Add(new ProgramContentWeekDto(
                weekNumber,
                weekStart,
                weekEnd,
                planRef,
                routineRef));
        }

        return new ProgramContentResponse(
            enrollment.Id,
            enrollment.PatientId,
            enrollment.TemplateId,
            enrollment.TotalWeeks,
            enrollment.StartLocalDate,
            weeks);
    }
}
