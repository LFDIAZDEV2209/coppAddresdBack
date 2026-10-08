using System.ComponentModel.DataAnnotations;
namespace CoppAddresd.Auth.Models;
public record PasswordRecoveryCodeRequest
{
    [Required, StringLength(50)] public string DocumentNumber { get; init; } = "";
    [Required, RegularExpression("^(email|phone)$")] public string ContactId { get; init; } = "";
    [Required, RegularExpression("^app$")] public string Application { get; init; } = "app";
}
public record PasswordRecoveryRequest : PasswordRecoveryCodeRequest
{
    [Required, RegularExpression("^[0-9]{6}$")] public string Otp { get; init; } = "";
    [Required, StringLength(128, MinimumLength = 8)] public string NewPassword { get; init; } = "";
}
