using CoppAddresd.Auth.Entities;

namespace CoppAddresd.Auth.Interfaces;

/// <summary>
/// Resultado de la búsqueda de un paciente por número de identificación.
/// </summary>
public record PatientLookupResult(
    Guid Id,
    Guid? UserId,
    string FirstName,
    string LastName,
    string DocumentNumber,
    string? Email,
    string? PhoneCountryCode,
    string? PhoneNumber);

/// <summary>
/// Búsqueda de pacientes en <c>app.patient_profiles</c> para el login por
/// número de identificación. Se implementa con SQL crudo porque el Auth
/// Service no posee el esquema <c>app.</c> (lo migra la API principal); solo
/// lo lee en la misma base de datos.
/// </summary>
public interface IPatientLookupService
{
    Task<PatientLookupResult?> FindByDocumentNumberAsync(
        string documentNumber,
        CancellationToken ct = default);
}
