using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.GetAdaptation;

/// <summary>
/// Consulta del detalle de una recomendación de adaptación (SPEC §7.7).
/// Recomendación inexistente → 404.
/// </summary>
public sealed record GetAdaptationQuery(
    Guid Id) : IRequest<AdaptationRecommendationDto>;

/// <summary>Lee la recomendación y la mapea al DTO (payload jsonb opaco).</summary>
public sealed class GetAdaptationQueryHandler(
    IProgramRepository repository) : IRequestHandler<GetAdaptationQuery, AdaptationRecommendationDto>
{
    public async Task<AdaptationRecommendationDto> Handle(GetAdaptationQuery request, CancellationToken ct)
    {
        var adaptation = await repository.GetAdaptationAsync(request.Id, ct)
            ?? throw new NotFoundException($"Recomendación de adaptación {request.Id} no encontrada.");

        return AdaptationRecommendationDto.FromEntity(adaptation);
    }
}