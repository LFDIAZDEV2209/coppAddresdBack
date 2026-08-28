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

        var weeks = new List<ProgramContentWeekDto>(enrollment.TotalWeeks);

        for (var weekNumber = 1; weekNumber <= enrollment.TotalWeeks; weekNumber++)
        {
            var weekStart = enrollment.StartLocalDate.AddDays((weekNumber - 1) * 7);
            var weekEnd = weekStart.AddDays(6);

            // Resolver contenido al inicio de la ventana (SPEC §4.2/§4.3):
            // la asignación activa que cubre el start de la semana gana.
            var resolution = await contentResolver.ResolveAsync(
                enrollment.PatientId, weekStart, ct);

            ProgramContentPlanRef? planRef = null;
            if (resolution?.NutritionPlanId is { } planId)
            {
                // Cargar nombre del plan (anti N+1: batch por ID).
                var planName = await programRepository.GetPlanNameAsync(planId, ct);
                planRef = new ProgramContentPlanRef(planId, planName?.Name ?? "unknown", planName?.Name ?? "unknown");
            }

            ProgramContentRoutineRef? routineRef = null;
            if (resolution?.ExerciseRoutineId is { } routineId)
            {
                var routineName = await programRepository.GetRoutineNameAsync(routineId, ct);
                routineRef = new ProgramContentRoutineRef(routineId, routineName?.Name ?? "unknown", routineName?.Name ?? "unknown");
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
