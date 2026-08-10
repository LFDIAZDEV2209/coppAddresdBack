using System.ComponentModel.DataAnnotations;

namespace CoppAddresd.Auth.Models;

public record CreateRoleRequest
{
    [Required(ErrorMessage = "Name es requerido")]
    [MaxLength(100)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; init; }
}
