namespace CoppAddresd.Telemedicine.Infrastructure.Configuration;

/// <summary>
/// Configuración del cliente hacia el backend del ERP (datos de referencia).
/// Espejo del patrón <c>AuthServiceSettings</c> del backend: base URL + clave
/// interna compartida (header <c>X-Internal-Key</c>). La clave real nunca se
/// commitea (appsettings gitignoreado).
/// </summary>
public class BackendServiceSettings
{
    public const string SectionName = "Backend";

    /// <summary>Base URL del backend (ej. http://localhost:5122).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Clave interna compartida (header X-Internal-Key). Nunca commitear un valor real.</summary>
    public string InternalApiKey { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 10;
}
