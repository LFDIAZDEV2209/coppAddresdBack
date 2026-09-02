using CoppAddresd.Application.Features.ProgramProgress.DTOs.Erp;
using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.Erp;

/// <summary>Stream de biometría para exporte CSV.</summary>
public sealed record StreamBiometriaPatientsExportQuery : IStreamRequest<BiometriaPatientListItemDto>;

public sealed class StreamBiometriaPatientsExportQueryHandler(IProgramRepository repository)
    : IStreamRequestHandler<StreamBiometriaPatientsExportQuery, BiometriaPatientListItemDto>
{
    public IAsyncEnumerable<BiometriaPatientListItemDto> Handle(
        StreamBiometriaPatientsExportQuery request, CancellationToken ct)
        => repository.StreamBiometriaPatientsForExportAsync(ct);
}
