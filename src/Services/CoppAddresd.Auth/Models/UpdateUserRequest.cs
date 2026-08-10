using System.ComponentModel.DataAnnotations;

namespace CoppAddresd.Auth.Models;

public record UpdateUserRequest
{
    [MaxLength(100)]
    public string? FirstName { get; init; }

    [MaxLength(100)]
    public string? LastName { get; init; }

    public bool? IsActive { get; init; }
}
