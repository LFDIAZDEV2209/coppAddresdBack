namespace CoppAddresd.Auth.Configuration;

/// <summary>
/// Configuración de Twilio Verify V2 para el envío y verificación de códigos
/// OTP por SMS. Las credenciales (AccountSid, ApiKeySid, ApiKeySecret) son
/// secretos: deben venir de variables de entorno o del gestor de secretos en
/// producción, nunca del código ni de appsettings versionados.
/// </summary>
public class TwilioSettings
{
    public const string SectionName = "Twilio";

    /// <summary>
    /// Habilita/deshabilita la integración con Twilio. Cuando es false (valor
    /// por defecto) la aplicación arranca sin exigir credenciales, de modo que
    /// Development no falla por falta de configuración. Cuando es true, las
    /// cuatro propiedades siguientes son obligatorias y se validan al arrancar.
    /// </summary>
    public bool IsEnabled { get; set; }

    public string AccountSid { get; set; } = string.Empty;

    /// <summary>
    /// API Key SID (creada en el Consola de Twilio). Se usa junto a
    /// <see cref="ApiKeySecret"/> para autenticar las llamadas a la API de
    /// Verify en lugar del Auth Token maestro.
    /// </summary>
    public string ApiKeySid { get; set; } = string.Empty;

    /// <summary>API Key Secret asociado al <see cref="ApiKeySid"/>.</summary>
    public string ApiKeySecret { get; set; } = string.Empty;

    /// <summary>SID del Verify Service creado en Twilio (servicio de verificación).</summary>
    public string VerifyServiceSid { get; set; } = string.Empty;
}
