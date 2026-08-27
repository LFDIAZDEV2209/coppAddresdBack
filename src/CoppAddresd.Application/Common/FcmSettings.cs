namespace CoppAddresd.Application.Common;

/// <summary>
/// Configuración de Firebase Cloud Messaging (FCM API v1). En desarrollo se
/// mantiene <c>Enabled=false</c> hasta configurar Firebase; el envío se degrada
/// a un resultado "disabled" sin romper el flujo del endpoint.
/// </summary>
public class FcmSettings
{
    public const string SectionName = "Fcm";

    /// <summary>Project id de Firebase (se usa en la URL de la API v1).</summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// JSON del service account (JSON crudo, base64 o ruta a archivo).
    /// Campos usados: <c>client_email</c>, <c>private_key</c>, <c>token_uri</c>.
    /// </summary>
    public string ServiceAccountJson { get; set; } = string.Empty;

    /// <summary>
    /// Habilita el envío real. <c>false</c> = operación degradada sin
    /// credenciales (dev hasta configurar Firebase).
    /// </summary>
    public bool Enabled { get; set; } = false;
}