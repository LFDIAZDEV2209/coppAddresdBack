using System.ComponentModel.DataAnnotations;

namespace CoppAddresd.Auth.Models;

public record LoginRequest
{
    /// <summary>
    /// Correo del usuario. Alternativa a <see cref="DocumentNumber"/>: el login
    /// acepta correo (staff/ERP) o número de identificación (pacientes, app).
    /// Debe enviarse uno de los dos.
    /// </summary>
    [EmailAddress(ErrorMessage = "Email inválido")]
    public string? Email { get; init; }

    /// <summary>
    /// Número de identificación del paciente (login de la app móvil). Se
    /// resuelve contra <c>app.patient_profiles</c> para localizar al usuario
    /// vinculado.
    /// </summary>
    [StringLength(50, ErrorMessage = "Número de identificación inválido")]
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
