using System.ComponentModel.DataAnnotations;

namespace CoppAddresd.Auth.Models;

public record StartDeletionSessionFromCodeRequest
{
    [Required(ErrorMessage = "Code es requerido")]
    [MaxLength(128)]
    public string Code { get; init; } = string.Empty;
}

public record StartDeletionSessionWithPasswordRequest
{
    [Required(ErrorMessage = "DocumentNumber es requerido")]
    [MaxLength(50)]
    public string DocumentNumber { get; init; } = string.Empty;

    [Required(ErrorMessage = "Password es requerido")]
    [MaxLength(256)]
    public string Password { get; init; } = string.Empty;
}

public record StartDeletionSessionWithOtpRequest
{
    [Required(ErrorMessage = "DocumentNumber es requerido")]
    [MaxLength(50)]
    public string DocumentNumber { get; init; } = string.Empty;

    [Required(ErrorMessage = "Otp es requerido")]
    [MaxLength(16)]
    public string Otp { get; init; } = string.Empty;
}

public record ConfirmDeletionSessionRequest
{
    /// <summary>Palabra canónica de confirmación: "ELIMINAR".</summary>
    [Required(ErrorMessage = "Confirmation es requerido")]
    [MaxLength(32)]
    public string Confirmation { get; init; } = string.Empty;
}

/// <summary>Lo único que ve el navegador: datos para mostrar la cuenta, nunca un token.</summary>
public record DeletionSessionSummary(
    string FirstName,
    string LastName,
    string? Email,
    DateTime ExpiresAt);

public record DeletionSessionConfirmed(DateTime? PurgeAfter, int RetentionDays);
