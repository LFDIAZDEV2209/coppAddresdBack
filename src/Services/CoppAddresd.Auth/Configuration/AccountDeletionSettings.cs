namespace CoppAddresd.Auth.Configuration;

/// <summary>Configuración del flujo web de eliminación de cuenta.</summary>
public class AccountDeletionSettings
{
    public const string SectionName = "AccountDeletion";

    /// <summary>
    /// Orígenes de la web de eliminación (p. ej. https://www.coppadresd.com).
    /// Vacío = el flujo web queda deshabilitado (falla cerrado).
    /// </summary>
    public string[] AllowedOrigins { get; set; } = [];

    /// <summary>Código de la aplicación cuya cuenta se elimina desde la web.</summary>
    public string Application { get; set; } = "app";

    /// <summary>Vida de la sesión de eliminación (cookie HttpOnly) en minutos.</summary>
    public int SessionMinutes { get; set; } = 10;
}
