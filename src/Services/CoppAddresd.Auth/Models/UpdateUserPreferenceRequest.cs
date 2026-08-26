using System.ComponentModel.DataAnnotations;

namespace CoppAddresd.Auth.Models;

public record UpdateUserPreferenceRequest
{
    [Required]
    [MaxLength(2)]
    public string Lang { get; init; } = string.Empty;
}
