namespace CoppAddresd.Auth.Models;

/// <summary>
/// Método de contacto asociado al número de identificación. El valor se
/// devuelve enmascarado para que el usuario pueda identificarlo sin exponer
/// el dato completo antes de verificar la propiedad del número.
/// </summary>
public record ContactMethodResponse(
    string Id,
    string Type,
    string Label);

/// <summary>
/// Resultado de la consulta por número de identificación: datos básicos del
/// paciente + los métodos de contacto disponibles para enviar el OTP.
/// </summary>
public record IdLookupResponse(
    Guid PatientId,
    string FirstName,
    string LastName,
    string DocumentNumber,
    IReadOnlyList<ContactMethodResponse> Contacts);
