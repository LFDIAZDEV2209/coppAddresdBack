using System.ComponentModel.DataAnnotations;

namespace CoppAddresd.Auth.Models;

/// <summary>
/// Verificación del código OTP del primer inicio de sesión. Si el código es
/// correcto, el usuario se provisiona (se crea su cuenta si no existe, se
/// vincula al perfil del paciente y se le otorga acceso a la aplicación) y se
/// emiten los tokens de sesión.
/// </summary>
public record VerifyOtpRequest
{
    [Required(ErrorMessage = "El número de identificación es requerido")]
    [StringLength(50, ErrorMessage = "Número de identificación inválido")]
    public string DocumentNumber { get; init; } = string.Empty;

    [Required(ErrorMessage = "El código es requerido")]
    [StringLength(10, MinimumLength = 4, ErrorMessage = "Código inválido")]
    public string Otp { get; init; } = string.Empty;

    /// <summary>
    /// Código de la aplicación con la que se autentica el usuario ("app").
    /// Determina el claim `aud` del JWT.
    /// </summary>
    [Required(ErrorMessage = "Application es requerido")]
    public string Application { get; init; } = string.Empty;

    /// <summary>
    /// True: cookie de refresh persistente (7 días). False: cookie de sesión (8 horas).
    /// </summary>
    public bool RememberMe { get; init; }
}
