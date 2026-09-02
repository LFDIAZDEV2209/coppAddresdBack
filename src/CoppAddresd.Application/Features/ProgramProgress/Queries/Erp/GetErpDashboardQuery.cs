using CoppAddresd.Application.Features.ProgramProgress.DTOs.Erp;
using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.Erp;

/// <summary>Dashboard ERP del monitoreo comunitario (SPEC §23, AC-50).</summary>
public sealed record GetErpDashboardQuery : IRequest<ProgramErpDashboardDto>;

public sealed class GetErpDashboardQueryHandler(IProgramRepository repository)
    : IRequestHandler<GetErpDashboardQuery, ProgramErpDashboardDto>
{
    public Task<ProgramErpDashboardDto> Handle(GetErpDashboardQuery request, CancellationToken ct)
        => repository.GetErpDashboardAsync(ct);
}
