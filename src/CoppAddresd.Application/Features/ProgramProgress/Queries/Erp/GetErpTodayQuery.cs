using CoppAddresd.Application.Features.ProgramProgress.DTOs.Erp;
using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.Erp;

/// <summary>Vista de hoy para el ERP (SPEC §23, AC-51).</summary>
public sealed record GetErpTodayQuery : IRequest<ProgramErpTodayDto>;

public sealed class GetErpTodayQueryHandler(IProgramRepository repository)
    : IRequestHandler<GetErpTodayQuery, ProgramErpTodayDto>
{
    public Task<ProgramErpTodayDto> Handle(GetErpTodayQuery request, CancellationToken ct)
        => repository.GetErpTodayAsync(ct);
}
