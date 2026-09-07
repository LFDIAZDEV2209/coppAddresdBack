using CoppAddresd.Application.Features.HealthTests;
using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.HealthTests.Queries;

/// <summary>Geo para el mapa de Tests de Salud (ciudades con % alto riesgo).</summary>
public sealed record GetHealthTestsGeoQuery : IRequest<HealthTestsGeoDto>;

public sealed class GetHealthTestsGeoQueryHandler(IHealthTestRepository repository)
    : IRequestHandler<GetHealthTestsGeoQuery, HealthTestsGeoDto>
{
    public Task<HealthTestsGeoDto> Handle(GetHealthTestsGeoQuery request, CancellationToken ct) =>
        repository.GetGeoAsync(ct);
}
