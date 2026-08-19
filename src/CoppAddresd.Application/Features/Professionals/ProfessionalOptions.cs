namespace CoppAddresd.Application.Features.Professionals;

/// <summary>
/// Vocabularios cerrados del módulo de empleados/profesionales. Valores
/// guardados como códigos estables en inglés; el frontend muestra etiquetas
/// en español. No son catálogos en BD porque son vocabularios pequeños y
/// estables (mismo criterio que <c>PatientOptions</c>).
/// </summary>
public static class ProfessionalOptions
{
    /// <summary>Estados del ciclo de vida del empleado.</summary>
    public static readonly IReadOnlyList<string> EmployeeStatuses =
        ["Invited", "Active", "Inactive"];

    /// <summary>Estados de la asignación empleado ↔ clínica.</summary>
    public static readonly IReadOnlyList<string> AssignmentStatuses =
        ["Active", "Inactive"];

    /// <summary>
    /// Tipos de credencial/licencia del profesional (taxonomía USA). La
    /// especialidad certificada va como FK opcional en la licencia.
    /// </summary>
    public static readonly IReadOnlyList<string> LicenseTypes =
        ["StateLicense", "BoardCertification", "DeaRegistration", "Npi", "CdrLicense", "NbcHwcCertification", "BlsAcls", "Other"];

    /// <summary>Estados de verificación de una credencial.</summary>
    public static readonly IReadOnlyList<string> VerificationStatuses =
        ["Pending", "Verified", "Expired", "Revoked"];

    public static bool IsAllowed(IReadOnlyList<string> allowed, string? value)
        => string.IsNullOrWhiteSpace(value) || allowed.Contains(value);

    public static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
