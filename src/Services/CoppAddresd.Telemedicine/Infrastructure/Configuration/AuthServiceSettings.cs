namespace CoppAddresd.Telemedicine.Infrastructure.Configuration;

/// <summary>
/// Configuración del cliente de introspección de permisos hacia el Auth
/// Service (<c>AuthService:BaseUrl</c> + clave interna <c>X-Internal-Key</c>).
/// El micro valida los permisos efectivos (claims ∪ scoped por clínica) como
/// el backend del ERP; la clave real nunca se commitea (appsettings
/// gitignoreado).
/// </summary>
public class AuthServiceSettings
{
    public const string SectionName = "AuthService";

    /// <summary>Base URL del Auth Service (ej. http://localhost:5123).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Clave interna compartida (header X-Internal-Key). Nunca commitear un valor real.</summary>
    public string InternalApiKey { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 10;
}