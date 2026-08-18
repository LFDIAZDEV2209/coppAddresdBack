using CoppAddresd.Application.Features.Catalogs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

/// <summary>
/// Catálogos del módulo de pacientes: geográficos (países, estados, ciudades,
/// códigos postales) y administrativos/clínicos (tipos de documento, etnias,
/// grupos sanguíneos, ICD-10, medicamentos, alergenos). Los endpoints de
/// búsqueda usan autocomplete (ILIKE con índice trigram).
/// </summary>
[ApiController]
[Route("api/v1/catalogs")]
[Authorize]
public class CatalogsController(IMediator mediator) : ControllerBase
{
    [HttpGet("countries")]
    public async Task<ActionResult<IReadOnlyList<CountryDto>>> Countries(CancellationToken ct)
        => Ok(await mediator.Send(new ListCountriesQuery(), ct));

    [HttpGet("states")]
    public async Task<ActionResult<IReadOnlyList<StateDto>>> States(
        [FromQuery] Guid countryId, CancellationToken ct)
        => Ok(await mediator.Send(new ListStatesQuery(countryId), ct));

    [HttpGet("cities")]
    public async Task<ActionResult<IReadOnlyList<CityDto>>> Cities(
        [FromQuery] Guid stateId,
        [FromQuery] string? search = null,
        [FromQuery] int limit = 30,
        CancellationToken ct = default)
        => Ok(await mediator.Send(new SearchCitiesQuery(stateId, search, limit), ct));

    [HttpGet("postal-codes")]
    public async Task<ActionResult<IReadOnlyList<PostalCodeDto>>> PostalCodes(
        [FromQuery] Guid cityId, CancellationToken ct)
        => Ok(await mediator.Send(new ListPostalCodesQuery(cityId), ct));

    /// <summary>
    /// Autocompletado escalable de códigos postales: consulta un proveedor
    /// externo (Zippopotam, con caché) con fallback a la BD local. Combina
    /// país + estado + ciudad y/o código postal (parcial o completo).
    /// </summary>
    [HttpGet("postal-codes/search")]
    public async Task<ActionResult<IReadOnlyList<PostalCodeSearchDto>>> SearchPostalCodes(
        [FromQuery] string? countryCode,
        [FromQuery] string? stateCode,
        [FromQuery] string? city,
        [FromQuery] string? zip,
        CancellationToken ct)
        => Ok(await mediator.Send(
            new SearchPostalCodesQuery(countryCode, stateCode, city, zip), ct));

    [HttpGet("blood-types")]
    public async Task<ActionResult<IReadOnlyList<CatalogOptionDto>>> BloodTypes(CancellationToken ct)
        => Ok(await mediator.Send(new ListBloodTypesQuery(), ct));

    [HttpGet("document-types")]
    public async Task<ActionResult<IReadOnlyList<CatalogOptionDto>>> DocumentTypes(CancellationToken ct)
        => Ok(await mediator.Send(new ListDocumentTypesQuery(), ct));

    [HttpGet("ethnicities")]
    public async Task<ActionResult<IReadOnlyList<CatalogOptionDto>>> Ethnicities(CancellationToken ct)
        => Ok(await mediator.Send(new ListEthnicitiesQuery(), ct));

    [HttpGet("icd10-codes")]
    public async Task<ActionResult<IReadOnlyList<CatalogSearchItemDto>>> Icd10Codes(
        [FromQuery] string? search = null,
        [FromQuery] int limit = 30,
        CancellationToken ct = default)
        => Ok(await mediator.Send(new SearchIcd10CodesQuery(search, limit), ct));

    [HttpGet("medications")]
    public async Task<ActionResult<IReadOnlyList<CatalogSearchItemDto>>> Medications(
        [FromQuery] string? search = null,
        [FromQuery] int limit = 30,
        CancellationToken ct = default)
        => Ok(await mediator.Send(new SearchMedicationsQuery(search, limit), ct));

    [HttpGet("allergens")]
    public async Task<ActionResult<IReadOnlyList<CatalogSearchItemDto>>> Allergens(
        [FromQuery] string? search = null,
        [FromQuery] int limit = 30,
        CancellationToken ct = default)
        => Ok(await mediator.Send(new SearchAllergensQuery(search, limit), ct));
}