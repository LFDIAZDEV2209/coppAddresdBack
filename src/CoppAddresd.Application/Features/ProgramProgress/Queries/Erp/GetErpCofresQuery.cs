using CoppAddresd.Application.Features.ProgramProgress.DTOs.Erp;
using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.Erp;

/// <summary>Vista de cofres/rachas ERP (SPEC §23, AC-53).</summary>
public sealed record GetErpCofresQuery(
    int Page = 1,
    int PageSize = 10,
    string? Search = null,
    string? SortBy = null,
    string? SortDir = null) : IRequest<ProgramErpCofresDto>;

public sealed class GetErpCofresQueryHandler(IProgramRepository repository)
    : IRequestHandler<GetErpCofresQuery, ProgramErpCofresDto>
{
    public Task<ProgramErpCofresDto> Handle(GetErpCofresQuery request, CancellationToken ct)
        => repository.GetErpCofresAsync(request.Page, request.PageSize, request.Search, request.SortBy, request.SortDir, ct);
}
