using CoppAddresd.Application.Features.ProgramProgress.DTOs.Erp;
using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.Erp;

/// <summary>Vista de adherencia ERP (SPEC §23, AC-52).</summary>
public sealed record GetErpAdherenciaQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? SortBy = null,
    string? SortDir = null) : IRequest<ProgramErpAdherenciaDto>;

public sealed class GetErpAdherenciaQueryHandler(IProgramRepository repository)
    : IRequestHandler<GetErpAdherenciaQuery, ProgramErpAdherenciaDto>
{
    public Task<ProgramErpAdherenciaDto> Handle(GetErpAdherenciaQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        return repository.GetErpAdherenciaAsync(page, pageSize, request.Search, request.SortBy, request.SortDir, ct);
    }
}
