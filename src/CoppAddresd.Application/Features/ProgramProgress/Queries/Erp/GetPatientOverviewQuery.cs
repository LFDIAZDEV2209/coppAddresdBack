using CoppAddresd.Application.Features.ProgramProgress.DTOs.Erp;
using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.Erp;

/// <summary>Perfil 360 de un paciente (SPEC §23, AC-54, anti-IDOR).</summary>
public sealed record GetPatientOverviewQuery(Guid PatientId) : IRequest<PatientOverviewDto?>;

public sealed class GetPatientOverviewQueryHandler(IProgramRepository repository)
    : IRequestHandler<GetPatientOverviewQuery, PatientOverviewDto?>
{
    public Task<PatientOverviewDto?> Handle(GetPatientOverviewQuery request, CancellationToken ct)
        => repository.GetPatientOverviewAsync(request.PatientId, ct);
}
