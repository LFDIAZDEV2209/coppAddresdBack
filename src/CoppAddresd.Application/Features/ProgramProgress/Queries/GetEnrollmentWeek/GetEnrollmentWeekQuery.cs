using CoppAddresd.Application.DTOs.ProgramProgress;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.GetEnrollmentWeek;

/// <summary>
/// Consulta del detalle de una semana específica de una inscripción
/// (<c>GET /program/enrollments/{id}/week/{weekNumber}</c>): tareas
/// programadas, completaciones, puntos y contenido activo (plan/rutina).
/// Requiere permiso <c>Program.View</c> con alcance clínico (404 si la
/// inscripción no existe o el paciente no está asignado al profesional).
/// </summary>
public sealed record GetEnrollmentWeekQuery(
    Guid EnrollmentId,
    int WeekNumber,
    Guid ClinicianUserId) : IRequest<EnrollmentWeekDetailDto?>;
