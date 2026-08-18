namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Resultado de una búsqueda de códigos postales vía proveedor externo
/// (p. ej. Zippopotam) o fallback a la BD local. <see cref="CityId"/> se
/// rellena cuando la ciudad devuelta existe en el catálogo local, para que el
/// frontend pueda auto-seleccionar la ciudad sin búsquedas adicionales.
/// </summary>
public sealed record PostalCodeLookupResult(
    string ZipCode,
    string City,
    string StateCode,
    string CountryCode,
    Guid? CityId = null);

/// <summary>
/// Búsqueda escalable de códigos postales: NO se pretende almacenar todos los
/// códigos postales del mundo en BD. La implementación consulta un proveedor
/// externo (cacheando resultados) y degrada a la tabla local de códigos
/// postales cuando el proveedor no está disponible (fail-open).
/// </summary>
public interface IPostalCodeLookupService
{
    /// <summary>
    /// Busca el lugar al que pertenece un código postal (código completo).
    /// Devuelve lista vacía si el código no existe o no es resolvible.
    /// </summary>
    Task<IReadOnlyList<PostalCodeLookupResult>> SearchByZipAsync(
        string countryCode,
        string zip,
        CancellationToken ct);

    /// <summary>
    /// Lista los códigos postales de una ciudad (país + estado + ciudad).
    /// Devuelve lista vacía si no hay coincidencias.
    /// </summary>
    Task<IReadOnlyList<PostalCodeLookupResult>> SearchByCityAsync(
        string countryCode,
        string stateCode,
        string city,
        CancellationToken ct);
}
