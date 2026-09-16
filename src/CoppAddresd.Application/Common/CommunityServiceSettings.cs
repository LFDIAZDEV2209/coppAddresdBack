namespace CoppAddresd.Application.Common;

/// <summary>
/// Configuración del canal interno ERP → Community (SPEC A13). El ERP envía un
/// mensaje directo al perfil comunitario del paciente usando la clave interna
/// compartida; el paciente lo ve en su app (bandeja de comunidad).
/// </summary>
public class CommunityServiceSettings
{
    public const string SectionName = "Community";

    public string BaseUrl { get; set; } = "http://localhost:5200";
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>Clave compartida para el endpoint interno de Community.</summary>
    public string InternalApiKey { get; set; } = string.Empty;

    /// <summary>Ruta del endpoint interno de mensaje directo a un usuario.</summary>
    public string InternalDirectMessageEndpoint { get; set; } = "/api/internal/messages/direct";
}
