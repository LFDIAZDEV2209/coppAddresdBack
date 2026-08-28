using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.GetPath;

/// <summary>
/// Consulta del sendero completo de semanas (SPEC §7.4), usado por la vista
/// "sendero" del móvil. La inscripción no existe → 404 (repositorio).
/// </summary>
public sealed record GetPathQuery(
    Guid EnrollmentId) : IRequest<ProgramPathDto>;

/// <summary>Delega en el repositorio; la proyección ya viene con el shape §7.4.</summary>
public sealed class GetPathQueryHandler(
    IProgramRepository repository) : IRequestHandler<GetPathQuery, ProgramPathDto>
{
    public async Task<ProgramPathDto> Handle(GetPathQuery request, CancellationToken ct)
        => await repository.GetPathAsync(request.EnrollmentId, ct);
}