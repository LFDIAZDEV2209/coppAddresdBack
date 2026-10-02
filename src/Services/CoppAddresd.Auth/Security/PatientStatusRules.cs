namespace CoppAddresd.Auth.Security;

/// <summary>
/// Reglas de estado del paciente aplicadas al acceso a la app móvil. El estado
/// vive en <c>app.patient_profiles.status</c> (lo posee la API principal): un
/// perfil con estado explícitamente <c>"Inactivo"</c> no puede iniciar sesión
/// ni refrescar tokens. Valores nulos, vacíos o desconocidos NO bloquean, para
/// no dejar fuera a pacientes legados sin estado.
/// </summary>
public static class PatientStatusRules
{
    /// <summary>Estado operativo que bloquea el acceso a la app móvil.</summary>
    public const string InactiveStatus = "Inactivo";

    /// <summary>
    /// Devuelve <c>true</c> solo cuando el estado es exactamente
    /// <c>"Inactivo"</c> (comparación ordinal, sensible a mayúsculas/minúsculas).
    /// </summary>
    public static bool BlocksAppAccess(string? status) =>
        string.Equals(status, InactiveStatus, StringComparison.Ordinal);
}
