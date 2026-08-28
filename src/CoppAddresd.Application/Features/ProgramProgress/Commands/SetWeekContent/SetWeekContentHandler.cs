using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Exceptions;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.SetWeekContent;

/// <summary>
/// Handler de <see cref="SetWeekContentCommand"/> (T-77): configura el contenido
/// de nutrición y ejercicio para una semana específica de la inscripción.
///
/// Comportamiento:
/// - nutritionPlanId proporcionado → upsert Active nutrition_plan_assignments
///   para (patient, plan) cubriendo exactamente [weekStart, weekEnd]; cualquier
///   asignación previa superpuesta se marca Completed (el status más cercano a
///   "superseded" disponible en el enum).
/// - nutritionPlanId null → desasigna: marca la asignación superpuesta como
///   Completed.
/// - Misma lógica para exerciseRoutineId → routine_assignments.
/// - Valida weekNumber ∈ [1..totalWeeks], plan/routine ids deben existir.
/// </summary>
public sealed class SetWeekContentHandler(
    IProgramRepository programRepository,
    IWellnessRepository wellnessRepository) : IRequestHandler<SetWeekContentCommand, ProgramContentWeekDto>
{
    public async Task<ProgramContentWeekDto> Handle(
        SetWeekContentCommand request, CancellationToken ct)
    {
        // 1. Validar inscripción.
        var enrollment = await programRepository.GetEnrollmentAsync(request.EnrollmentId, ct)
            ?? throw new NotFoundException($"Inscripción {request.EnrollmentId} no encontrada.");

        // 2. Validar weekNumber.
        if (request.WeekNumber < 1 || request.WeekNumber > enrollment.TotalWeeks)
        {
            throw new UnprocessableEntityException(
                $"WEEK_NUMBER_OUT_OF_RANGE: el número de semana {request.WeekNumber} " +
                $"está fuera del rango [1..{enrollment.TotalWeeks}].");
        }

        var weekStart = enrollment.StartLocalDate.AddDays((request.WeekNumber - 1) * 7);
        var weekEnd = weekStart.AddDays(6);
        var now = DateTime.UtcNow;
        var weekStartUtc = weekStart.ToDateTime(TimeOnly.MinValue);
        var weekEndUtc = weekEnd.ToDateTime(TimeOnly.MaxValue);

        // 3. Nutrición: upsert o desasignar.
        ProgramContentPlanRef? planRef = null;
        if (request.NutritionPlanId is { } planId)
        {
            // Validar que el plan existe.
            var plan = await wellnessRepository.GetPlanByIdAsync(planId, ct)
                ?? throw new NotFoundException($"Plan de alimentación {planId} no encontrado.");

            // Marcar como Completed cualquier asignación previa superpuesta
            // del mismo paciente y plan.
            var overlapping = await wellnessRepository.ListPlanAssignmentsByPatientAsync(enrollment.PatientId, ct);
            foreach (var existing in overlapping.Where(a =>
                a.Status == AssignmentStatus.Active
                && a.PlanId == planId
                && DatesOverlap(a.StartDate, a.EndDate, weekStartUtc, weekEndUtc)))
            {
                existing.Status = AssignmentStatus.Completed;
                existing.EndDate = weekStartUtc.AddDays(-1);
                existing.UpdatedAt = now;
                await wellnessRepository.UpdatePlanAssignmentAsync(existing, ct);
            }

            // Crear la nueva asignación exacta [weekStart, weekEnd].
            var assignment = new NutritionPlanAssignment
            {
                Id = Guid.NewGuid(),
                PatientId = enrollment.PatientId,
                PlanId = planId,
                StartDate = weekStartUtc,
                EndDate = weekEndUtc,
                Status = AssignmentStatus.Active,
                CreatedAt = now,
            };
            await wellnessRepository.AddPlanAssignmentAsync(assignment, ct);

            planRef = new ProgramContentPlanRef(plan.Id, plan.Name, plan.Name);
        }
        else
        {
            // Desasignar: marcar como Completed cualquier asignación superpuesta.
            var overlapping = await wellnessRepository.ListPlanAssignmentsByPatientAsync(enrollment.PatientId, ct);
            foreach (var existing in overlapping.Where(a =>
                a.Status == AssignmentStatus.Active
                && DatesOverlap(a.StartDate, a.EndDate, weekStartUtc, weekEndUtc)))
            {
                existing.Status = AssignmentStatus.Completed;
                existing.EndDate = weekStartUtc.AddDays(-1);
                existing.UpdatedAt = now;
                await wellnessRepository.UpdatePlanAssignmentAsync(existing, ct);
            }
        }

        // 4. Ejercicio: upsert o desasignar.
        ProgramContentRoutineRef? routineRef = null;
        if (request.ExerciseRoutineId is { } routineId)
        {
            // Validar que la rutina existe.
            var routine = await wellnessRepository.GetRoutineByIdAsync(routineId, ct)
                ?? throw new NotFoundException($"Rutina de ejercicio {routineId} no encontrada.");

            // Marcar como Completed cualquier asignación previa superpuesta
            // del mismo paciente y rutina.
            var overlapping = await wellnessRepository.ListAssignmentsByPatientAsync(enrollment.PatientId, ct);
            foreach (var existing in overlapping.Where(a =>
                a.Status == AssignmentStatus.Active
                && a.RoutineId == routineId
                && DatesOverlap(a.StartDate, a.EndDate, weekStartUtc, weekEndUtc)))
            {
                existing.Status = AssignmentStatus.Completed;
                existing.EndDate = weekStartUtc.AddDays(-1);
                existing.UpdatedAt = now;
                await wellnessRepository.UpdateAssignmentAsync(existing, ct);
            }

            // Crear la nueva asignación exacta [weekStart, weekEnd].
            var assignment = new RoutineAssignment
            {
                Id = Guid.NewGuid(),
                PatientId = enrollment.PatientId,
                RoutineId = routineId,
                StartDate = weekStartUtc,
                EndDate = weekEndUtc,
                Status = AssignmentStatus.Active,
                CreatedAt = now,
            };
            await wellnessRepository.AddAssignmentAsync(assignment, ct);

            routineRef = new ProgramContentRoutineRef(routine.Id, routine.Name, routine.Name);
        }
        else
        {
            // Desasignar: marcar como Completed cualquier asignación superpuesta.
            var overlapping = await wellnessRepository.ListAssignmentsByPatientAsync(enrollment.PatientId, ct);
            foreach (var existing in overlapping.Where(a =>
                a.Status == AssignmentStatus.Active
                && DatesOverlap(a.StartDate, a.EndDate, weekStartUtc, weekEndUtc)))
            {
                existing.Status = AssignmentStatus.Completed;
                existing.EndDate = weekStartUtc.AddDays(-1);
                existing.UpdatedAt = now;
                await wellnessRepository.UpdateAssignmentAsync(existing, ct);
            }
        }

        return new ProgramContentWeekDto(
            request.WeekNumber,
            weekStart,
            weekEnd,
            planRef,
            routineRef);
    }

    /// <summary>
    /// Verifica si dos ventanas de fechas se superponen.
    /// </summary>
    private static bool DatesOverlap(DateTime start1, DateTime? end1, DateTime start2, DateTime end2)
    {
        var e1 = end1 ?? DateTime.MaxValue;
        return start1 <= end2 && start2 <= e1;
    }
}
