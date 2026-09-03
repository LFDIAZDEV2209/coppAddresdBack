using CoppAddresd.Application.Features.ProgramProgress.DTOs.Erp;
using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.Erp;

/// <summary>Detalle de biometría de un paciente (historial semanal, heatmap, datos exactos).</summary>
public sealed record GetBiometriaPatientQuery(Guid PatientId) : IRequest<BiometriaPatientDetailDto?>;

public sealed class GetBiometriaPatientQueryHandler(IProgramRepository repository)
    : IRequestHandler<GetBiometriaPatientQuery, BiometriaPatientDetailDto?>
{
    public Task<BiometriaPatientDetailDto?> Handle(GetBiometriaPatientQuery request, CancellationToken ct)
        => repository.GetBiometriaPatientAsync(request.PatientId, ct);
}
