namespace CoppAddresd.Api.Configuration;

/// <summary>
/// Configuración del servicio de Telemedicina visto desde el backend: base URL
/// y la clave interna compartida (header <c>X-Internal-Key</c>) con la que el
/// microservicio de Telemedicina consulta los datos de referencia. Espejo del
/// patrón <see cref="AuthServiceSettings"/>. La clave real nunca se commitea.
/// </summary>
public class TelemedicineServiceSettings
{
    public const string SectionName = "Telemedicine";

    /// <summary>Base URL del microservicio de Telemedicina (ej. http://localhost:5130).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Clave interna compartida (header X-Internal-Key). Nunca commitear un valor real.</summary>
    public string InternalApiKey { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 10;
}
