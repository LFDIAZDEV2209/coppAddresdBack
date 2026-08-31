using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Patients;

/// <summary>Lista el catálogo de aseguradoras (para filtros y formularios).</summary>
public record ListInsurersQuery : IRequest<IReadOnlyList<InsurerDto>>;

/// <summary>Cache-aside con TTL 1h (catálogo por seed). Clave: catalog:insurers:v1.</summary>
public sealed class ListInsurersQueryHandler(IPatientRepository repository, ICacheService cache)
    : IRequestHandler<ListInsurersQuery, IReadOnlyList<InsurerDto>>
{
    public async Task<IReadOnlyList<InsurerDto>> Handle(
        ListInsurersQuery request,
        CancellationToken ct
    )
    {
        return await cache.GetOrCreateAsync(
            CacheKeys.Catalog("insurers"),
            CacheKeys.CatalogTtl,
            async token =>
            {
                var insurers = await repository.ListInsurersAsync(token);
                return insurers.Select(InsurerDto.FromEntity).ToList();
            },
            ct
        );
    }
}
