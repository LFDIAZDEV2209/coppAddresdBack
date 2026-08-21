using System.ComponentModel.DataAnnotations;

namespace CoppAddresd.Auth.Models;

public record LoginRequest
{
    /// <summary>
    /// Correo electrónico del usuario. Opcional: se usa para el login del ERP.
    /// Para el login móvil de pacientes puede omitirse en favor de
    /// <see cref="DocumentNumber"/>.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Número de identificación del paciente (login alternativo de la app móvil
    /// con <c>application:"app"</c>). Cuando se informa, el usuario se resuelve
    /// a través de <c>app.patient_profiles</c> en lugar del correo.
    /// </summary>
    public string? DocumentNumber { get; init; }

    [Required(ErrorMessage = "Password es requerido")]
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// Código de la aplicación con la que se autentica el usuario ("erp", "app").
    /// Determina el claim `aud` del JWT. El acceso se valida contra
    /// <c>auth.user_applications</c>.
    /// </summary>
    [Required(ErrorMessage = "Application es requerido")]
    public string Application { get; init; } = string.Empty;

    /// <summary>
    /// True: cookie de refresh persistente (7 días). False: cookie de sesión (8 horas).
    /// </summary>
    public bool RememberMe { get; init; }
}