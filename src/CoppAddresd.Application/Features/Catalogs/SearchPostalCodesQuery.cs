using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Catalogs;

/// <summary>
/// Búsqueda de códigos postales para autocompletado del formulario. Fuente
/// primaria: proveedor externo (Zippopotam) con caché; degrada a la BD local
/// si el proveedor no responde (fail-open, nunca 500 al cliente).
/// </summary>
public record SearchPostalCodesQuery(
    string? CountryCode,
    string? StateCode,
    string? City,
    string? Zip)
    : IRequest<IReadOnlyList<PostalCodeSearchDto>>;

public sealed class SearchPostalCodesQueryHandler(
    IPostalCodeLookupService lookup) : IRequestHandler<SearchPostalCodesQuery, IReadOnlyList<PostalCodeSearchDto>>
{
    public async Task<IReadOnlyList<PostalCodeSearchDto>> Handle(
        SearchPostalCodesQuery request, CancellationToken ct)
    {
        var country = string.IsNullOrWhiteSpace(request.CountryCode)
            ? "US"
            : request.CountryCode.Trim().ToUpperInvariant();
        var zip = (request.Zip ?? string.Empty).Trim().Replace(" ", "");
        var city = request.City?.Trim() ?? string.Empty;
        var state = request.StateCode?.Trim().ToUpperInvariant() ?? string.Empty;

        IReadOnlyList<PostalCodeLookupResult> results;

        if (zip.Length > 0 && city.Length > 0 && state.Length > 0)
        {
            // ZIPs de la ciudad filtrados por prefijo (autocompletado local).
            var cityZips = await lookup.SearchByCityAsync(country, state, city, ct);
            results = cityZips
                .Where(r => r.ZipCode.StartsWith(zip, StringComparison.Ordinal))
                .ToList();
        }
        else if (zip.Length > 0)
        {
            results = await lookup.SearchByZipAsync(country, zip, ct);
        }
        else if (city.Length > 0 && state.Length > 0)
        {
            results = await lookup.SearchByCityAsync(country, state, city, ct);
        }
        else
        {
            return [];
        }

        return results
            .Select(r => new PostalCodeSearchDto(
                r.ZipCode, r.City, r.StateCode, r.CountryCode, r.CityId))
            .ToList();
    }
}
