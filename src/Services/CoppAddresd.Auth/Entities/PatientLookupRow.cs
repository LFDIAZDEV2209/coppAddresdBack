namespace CoppAddresd.Auth.Entities;

/// <summary>
/// Proyección de solo lectura de <c>app.patient_profiles</c> usada por el Auth
/// Service para el login por número de identificación. Se consulta con SQL
/// crudo (<c>SqlQueryRaw</c>) para no reclamar propiedad del esquema <c>app.</c>
/// (lo crea la API principal con sus propias migraciones).
/// </summary>
public class PatientLookupRow
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneCountryCode { get; set; }
    public string? PhoneNumber { get; set; }
}
