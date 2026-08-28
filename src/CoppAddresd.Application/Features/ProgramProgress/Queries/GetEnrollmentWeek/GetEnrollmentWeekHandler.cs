using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Interfaces;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.GetEnrollmentWeek;

/// <summary>
/// Handler de <see cref="GetEnrollmentWeekQuery"/>: resuelve el detalle de una
/// semana específica de una inscripción, incluyendo tareas programadas del
/// snapshot, completaciones reales, rollup diario y contenido activo (plan/rutina).
/// El scoping clínico (paciente asignado al profesional) se valida en el repositorio.
/// </summary>
public sealed class GetEnrollmentWeekHandler(
    IProgramRepository programRepository)
    : IRequestHandler<GetEnrollmentWeekQuery, EnrollmentWeekDetailDto?>
{
    public async Task<EnrollmentWeekDetailDto?> Handle(
        GetEnrollmentWeekQuery request, CancellationToken ct)
    {
        return await programRepository.GetEnrollmentWeekDetailAsync(
            request.EnrollmentId, request.WeekNumber, request.ClinicianUserId, ct);
    }
}
