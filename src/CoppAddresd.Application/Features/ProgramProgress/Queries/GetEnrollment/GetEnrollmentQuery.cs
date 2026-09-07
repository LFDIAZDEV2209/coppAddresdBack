using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.GetEnrollment;

/// <summary>
/// Consulta del detalle de una inscripción por id (ERP, TASK-10): mismo shape
/// del listado (<see cref="ProgramEnrollmentDto"/>) con el
/// <c>CurrentLevel</c> derivado de la XP. Devuelve null si la inscripción no
/// existe (el controller responde 404, anti-IDOR).
/// </summary>
public sealed record GetEnrollmentQuery(Guid EnrollmentId) : IRequest<ProgramEnrollmentDto?>;

/// <summary>Delega la proyección al repositorio (una sola query, sin N+1).</summary>
public sealed class GetEnrollmentQueryHandler(
    IProgramRepository repository) : IRequestHandler<GetEnrollmentQuery, ProgramEnrollmentDto?>
{
    public Task<ProgramEnrollmentDto?> Handle(GetEnrollmentQuery request, CancellationToken ct)
        => repository.GetEnrollmentAsync(request.EnrollmentId, ct);
}