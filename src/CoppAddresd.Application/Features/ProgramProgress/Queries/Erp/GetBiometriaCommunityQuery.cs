using CoppAddresd.Application.Features.ProgramProgress.DTOs.Erp;
using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.Erp;

/// <summary>Resumen comunitario de Biometría (dashboard comunitario).</summary>
public sealed record GetBiometriaCommunityQuery : IRequest<BiometriaCommunityDto>;

public sealed class GetBiometriaCommunityQueryHandler(IProgramRepository repository)
    : IRequestHandler<GetBiometriaCommunityQuery, BiometriaCommunityDto>
{
    public Task<BiometriaCommunityDto> Handle(GetBiometriaCommunityQuery request, CancellationToken ct)
        => repository.GetBiometriaCommunityAsync(ct);
}
