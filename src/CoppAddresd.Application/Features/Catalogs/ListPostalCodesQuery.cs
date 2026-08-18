using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Catalogs;

/// <summary>Lista los códigos postales (US ZIP) de una ciudad.</summary>
public record ListPostalCodesQuery(Guid CityId) : IRequest<IReadOnlyList<PostalCodeDto>>;

public sealed class ListPostalCodesQueryHandler(ICatalogRepository repository)
    : IRequestHandler<ListPostalCodesQuery, IReadOnlyList<PostalCodeDto>>
{
    public async Task<IReadOnlyList<PostalCodeDto>> Handle(
        ListPostalCodesQuery request, CancellationToken ct)
    {
        var codes = await repository.ListPostalCodesByCityAsync(request.CityId, ct);
        return codes.Select(p => new PostalCodeDto(p.Id, p.ZipCode)).ToList();
    }
}