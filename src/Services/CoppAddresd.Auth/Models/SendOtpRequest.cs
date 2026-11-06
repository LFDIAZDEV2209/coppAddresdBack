using System.ComponentModel.DataAnnotations;

namespace CoppAddresd.Auth.Models;

/// <summary>
/// Solicitud de envío de un código OTP para el primer inicio de sesión.
/// El destino se resuelve en el servidor a partir del contacto elegido
/// (el cliente nunca envía el correo/teléfono en claro).
/// </summary>
public record SendOtpRequest
{
    /// <summary>Idioma del mensaje (es/en o variante regional); omitido/desconocido usa español.</summary>
    [StringLength(35)]
    public string? Language { get; init; }

    [Required(ErrorMessage = "El número de identificación es requerido")]
    [StringLength(50, ErrorMessage = "Número de identificación inválido")]
    public string DocumentNumber { get; init; } = string.Empty;

    /// <summary>
    /// Identificador del método de contacto elegido (devuelto por
    /// <c>id-lookup</c>), ej. <c>"email"</c> o <c>"phone"</c>.
    /// </summary>
    [Required(ErrorMessage = "Debes elegir un método de contacto")]
    [StringLength(20, ErrorMessage = "Método de contacto inválido")]
    public string ContactId { get; init; } = string.Empty;
}
