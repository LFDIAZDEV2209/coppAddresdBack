namespace CoppAddresd.Api.Configuration;

/// <summary>Configuración del cliente hacia el Auth Service (introspección scoped).</summary>
public class AuthServiceSettings
{
    public const string SectionName = "AuthService";

    /// <summary>Base URL del Auth Service (ej. http://localhost:5123).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Clave interna compartida (header X-Internal-Key). Nunca commitear un valor real.</summary>
    public string InternalApiKey { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 10;
}
