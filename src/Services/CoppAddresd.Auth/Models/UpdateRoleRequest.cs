using System.ComponentModel.DataAnnotations;

namespace CoppAddresd.Auth.Models;

public record UpdateRoleRequest
{
    [MaxLength(100)]
    public string? Name { get; init; }

    [MaxLength(500)]
    public string? Description { get; init; }

    public bool? IsActive { get; init; }
}
