using CoppAddresd.Application.Features.ProgramProgress.DTOs.Interventions;
using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.ListInterventions;

/// <summary>
/// Intervenciones del paciente autenticado (SPEC §22, D): listado paginado
/// (default 20, orden descendente por <c>createdAt</c>) de las intervenciones
/// derivadas de debilidades y registradas por profesionales. El
/// <c>patientId</c> SIEMPRE llega resuelto de la identidad del JWT por la capa
/// API (nunca del body): es la base del anti-IDOR (AC-11).
/// </summary>
public sealed record ListInterventionsQuery(Guid PatientId, int Page = 1, int PageSize = 20)
    : IRequest<PaginatedInterventionsResult>;

/// <summary>Orquesta la lectura de las intervenciones del paciente (proyección, sin N+1).</summary>
public sealed class ListInterventionsQueryHandler(
    IProgramRepository repository) : IRequestHandler<ListInterventionsQuery, PaginatedInterventionsResult>
{
    public async Task<PaginatedInterventionsResult> Handle(
        ListInterventionsQuery request, CancellationToken ct)
    {
        var (items, total) = await repository.ListInterventionsAsync(
            request.PatientId, request.Page, request.PageSize, ct);

        return new PaginatedInterventionsResult(
            items, total, request.Page, request.PageSize,
            (int)Math.Ceiling(total / (double)Math.Max(1, request.PageSize)));
    }
}
