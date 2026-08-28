using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Professionals;

/// <summary>Lista el árbol organizacional completo (organizaciones → clínicas → sedes).</summary>
public record ListOrganizationTreeQuery : IRequest<IReadOnlyList<OrganizationTreeNodeDto>>;

public sealed class ListOrganizationTreeQueryHandler(IOrganizationRepository repository)
    : IRequestHandler<ListOrganizationTreeQuery, IReadOnlyList<OrganizationTreeNodeDto>>
{
    public async Task<IReadOnlyList<OrganizationTreeNodeDto>> Handle(
        ListOrganizationTreeQuery request,
        CancellationToken ct
    )
    {
        var organizations = await repository.ListTreeAsync(ct);
        return organizations
            .OrderBy(o => o.Name)
            .Select(OrganizationTreeNodeDto.FromEntity)
            .ToList();
    }
}

/// <summary>Lista el catálogo de profesiones con sus especialidades válidas.</summary>
public record ListProfessionalTypesQuery : IRequest<IReadOnlyList<ProfessionalTypeDto>>;

/// <summary>Cache-aside con TTL 1h. Invalidado por Create/UpdateProfessionalType.</summary>
public sealed class ListProfessionalTypesQueryHandler(
    IOrganizationRepository repository,
    ICacheService cache
) : IRequestHandler<ListProfessionalTypesQuery, IReadOnlyList<ProfessionalTypeDto>>
{
    public async Task<IReadOnlyList<ProfessionalTypeDto>> Handle(
        ListProfessionalTypesQuery request,
        CancellationToken ct
    )
    {
        return await cache.GetOrCreateAsync(
            CacheKeys.Catalog("professional-types"),
            CacheKeys.CatalogTtl,
            async token =>
            {
                var types = await repository.ListProfessionalTypesAsync(token);
                return types
                    .OrderBy(t => t.SortOrder)
                    .Select(ProfessionalTypeDto.FromEntity)
                    .ToList();
            },
            ct
        );
    }
}

/// <summary>Lista el catálogo de especialidades agrupado por categoría.</summary>
public record ListSpecialtiesQuery : IRequest<IReadOnlyList<SpecialtyDto>>;

/// <summary>Cache-aside con TTL 1h. Invalidado por Create/UpdateSpecialty.</summary>
public sealed class ListSpecialtiesQueryHandler(
    IOrganizationRepository repository,
    ICacheService cache
) : IRequestHandler<ListSpecialtiesQuery, IReadOnlyList<SpecialtyDto>>
{
    public async Task<IReadOnlyList<SpecialtyDto>> Handle(
        ListSpecialtiesQuery request,
        CancellationToken ct
    )
    {
        return await cache.GetOrCreateAsync(
            CacheKeys.Catalog("specialties"),
            CacheKeys.CatalogTtl,
            async token =>
            {
                var specialties = await repository.ListSpecialtiesAsync(token);
                return specialties
                    .OrderBy(s => s.Category)
                    .ThenBy(s => s.SortOrder)
                    .Select(SpecialtyDto.FromEntity)
                    .ToList();
            },
            ct
        );
    }
}
