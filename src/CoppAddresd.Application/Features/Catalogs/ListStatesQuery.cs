using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Catalogs;

/// <summary>Lista los estados de un país (ordenados por nombre).</summary>
public record ListStatesQuery(Guid CountryId) : IRequest<IReadOnlyList<StateDto>>;

public sealed class ListStatesQueryHandler(ICatalogRepository repository)
    : IRequestHandler<ListStatesQuery, IReadOnlyList<StateDto>>
{
    public async Task<IReadOnlyList<StateDto>> Handle(ListStatesQuery request, CancellationToken ct)
    {
        var states = await repository.ListStatesByCountryAsync(request.CountryId, ct);
        return states.Select(s => new StateDto(s.Id, s.Code, s.Name)).ToList();
    }
}