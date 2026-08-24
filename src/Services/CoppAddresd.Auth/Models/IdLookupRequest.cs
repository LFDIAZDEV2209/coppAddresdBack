using System.ComponentModel.DataAnnotations;

namespace CoppAddresd.Auth.Models;

/// <summary>
/// Consulta de un número de identificación para el primer inicio de sesión.
/// Devuelve los métodos de contacto (correos/teléfonos) asociados al paciente
/// para que el usuario elija por dónde recibe el código OTP.
/// </summary>
public record IdLookupRequest
{
    [Required(ErrorMessage = "El número de identificación es requerido")]
    [StringLength(50, ErrorMessage = "Número de identificación inválido")]
    public string DocumentNumber { get; init; } = string.Empty;

    /// <summary>
    /// Código de la aplicación con la que se autentica el usuario ("app").
    /// Determina el claim `aud` del JWT que se emitirá al verificar el OTP.
    /// </summary>
    [Required(ErrorMessage = "Application es requerido")]
    public string Application { get; init; } = string.Empty;
}
