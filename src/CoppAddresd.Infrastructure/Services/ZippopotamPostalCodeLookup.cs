using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Opciones de la búsqueda de códigos postales (sección <c>PostalCodeLookup</c>).
/// </summary>
public sealed class PostalCodeLookupOptions
{
    public const string SectionName = "PostalCodeLookup";

    /// <summary>Si es false, se salta el proveedor externo (solo BD local).</summary>
    public bool Enabled { get; set; } = true;

    public string BaseUrl { get; set; } = "https://api.zippopotam.us";

    public double TimeoutSeconds { get; set; } = 3;

    public int CacheTtlMinutes { get; set; } = 24 * 60;
}

/// <summary>
/// Proveedor de códigos postales basado en Zippopotam (gratuito, sin API key,
/// cobertura de EE. UU. y ~50 países) con caché en memoria y fail-open a la
/// tabla local de códigos postales. La BD local nunca es la fuente principal:
/// solo cubre los datos seed de desarrollo y el modo sin internet.
/// </summary>
public sealed class ZippopotamPostalCodeLookup(
    IHttpClientFactory httpClientFactory,
    IOptions<PostalCodeLookupOptions> options,
    IMemoryCache cache,
    AppDbContext db,
    ILogger<ZippopotamPostalCodeLookup> logger) : IPostalCodeLookupService
{
    private const string ClientName = "Zippopotam";

    public async Task<IReadOnlyList<PostalCodeLookupResult>> SearchByZipAsync(
        string countryCode, string zip, CancellationToken ct)
    {
        var cacheKey = $"zip:{countryCode}:{zip}";
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<PostalCodeLookupResult>? cached))
            return await EnrichAsync(cached!, ct);

        var external = await TryExternalAsync($"/{countryCode.ToLowerInvariant()}/{zip}", ct);
        if (external is not null)
        {
            cache.Set(cacheKey, external, TimeSpan.FromMinutes(GetCacheTtlMinutes()));
            return await EnrichAsync(external, ct);
        }

        // Fail-open: proveedor caído o deshabilitado → tabla local.
        var fallback = await db.PostalCodes
            .Where(p => p.ZipCode == zip &&
                        p.City!.State!.Country!.Code == countryCode)
            .Select(p => new PostalCodeLookupResult(
                p.ZipCode, p.City!.Name, p.City.State!.Code, countryCode, p.CityId))
            .ToListAsync(ct);
        return fallback;
    }

    public async Task<IReadOnlyList<PostalCodeLookupResult>> SearchByCityAsync(
        string countryCode, string stateCode, string city, CancellationToken ct)
    {
        var cacheKey = $"city:{countryCode}:{stateCode}:{city}";
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<PostalCodeLookupResult>? cached))
            return await EnrichAsync(cached!, ct);

        var external = await TryExternalAsync(
            $"/{countryCode.ToLowerInvariant()}/{stateCode}/{Uri.EscapeDataString(city)}",
            ct);
        if (external is not null)
        {
            cache.Set(cacheKey, external, TimeSpan.FromMinutes(GetCacheTtlMinutes()));
            return await EnrichAsync(external, ct);
        }

        var fallback = await db.PostalCodes
            .Where(p => p.City!.Name.ToLower() == city.ToLower() &&
                        p.City!.State!.Code == stateCode &&
                        p.City!.State!.Country!.Code == countryCode)
            .Select(p => new PostalCodeLookupResult(
                p.ZipCode, p.City!.Name, stateCode, countryCode, p.CityId))
            .ToListAsync(ct);
        return fallback;
    }

    /// <summary>
    /// Rellena <see cref="PostalCodeLookupResult.CityId"/> resolviendo cada
    /// lugar contra el catálogo local de ciudades (una sola consulta con IN).
    /// </summary>
    private async Task<IReadOnlyList<PostalCodeLookupResult>> EnrichAsync(
        IReadOnlyList<PostalCodeLookupResult> results, CancellationToken ct)
    {
        if (results.Count == 0 || results.All(r => r.CityId is not null))
            return results;

        var names = results
            .Select(r => r.City)
            .Distinct()
            .ToList();

        var candidates = await db.Cities
            .Where(c => names.Contains(c.Name))
            .Select(c => new
            {
                c.Id,
                c.Name,
                StateCode = c.State!.Code,
                CountryCode = c.State!.Country!.Code,
            })
            .ToListAsync(ct);

        var cityIdByKey = candidates
            .GroupBy(c => $"{c.CountryCode}:{c.StateCode}:{c.Name}")
            .ToDictionary(group => group.Key, group => group.First().Id);

        return results
            .Select(r => r.CityId ?? (
                cityIdByKey.TryGetValue($"{r.CountryCode}:{r.StateCode}:{r.City}", out var id)
                    ? id
                    : null))
            .Select((id, index) => results[index] with { CityId = id })
            .ToList();
    }

    /// <summary>
    /// Llama al proveedor externo. Devuelve null (no lanza) cuando el proveedor
    /// no puede resolver la búsqueda (404, timeout, error de red o deshabilitado)
    /// para que el llamador degrade a la BD local.
    /// </summary>
    private async Task<IReadOnlyList<PostalCodeLookupResult>?> TryExternalAsync(
        string path, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.Enabled)
            return null;

        try
        {
            var client = httpClientFactory.CreateClient(ClientName);
            var payload = await client.GetFromJsonAsync<ZippopotamResponse>(path, ct);

            // En búsquedas por ciudad el código postal vive en cada place;
            // por código vive a nivel raíz. Se usa place → raíz como fallback.
            return payload?.Places?
                .Select(p => new PostalCodeLookupResult(
                    p.PostCode ?? payload.PostCode ?? string.Empty,
                    p.PlaceName ?? payload.PlaceName ?? string.Empty,
                    p.StateAbbreviation ?? payload.StateAbbreviation ?? string.Empty,
                    payload.CountryAbbreviation ?? string.Empty))
                .Where(r => r.ZipCode.Length > 0)
                .ToList();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Código/lugar inexistente: no es un fallo del proveedor.
            return [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex,
                "Proveedor de códigos postales no disponible ({Path}); degradando a BD local.",
                path);
            return null;
        }
    }

    private int GetCacheTtlMinutes() => Math.Max(1, options.Value.CacheTtlMinutes);
}

/// <summary>Respuesta del servicio Zippopotam (nombres con espacios vía JSON).</summary>
internal sealed class ZippopotamResponse
{
    [JsonPropertyName("post code")]
    public string? PostCode { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("country abbreviation")]
    public string? CountryAbbreviation { get; set; }

    [JsonPropertyName("place name")]
    public string? PlaceName { get; set; }

    [JsonPropertyName("state abbreviation")]
    public string? StateAbbreviation { get; set; }

    [JsonPropertyName("places")]
    public List<ZippopotamPlace>? Places { get; set; }
}

internal sealed class ZippopotamPlace
{
    [JsonPropertyName("place name")]
    public string? PlaceName { get; set; }

    [JsonPropertyName("post code")]
    public string? PostCode { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("state abbreviation")]
    public string? StateAbbreviation { get; set; }
}
