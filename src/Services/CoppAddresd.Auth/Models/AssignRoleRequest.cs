using System.ComponentModel.DataAnnotations;

namespace CoppAddresd.Auth.Models;

public record AssignRoleRequest
{
    [Required(ErrorMessage = "RoleId es requerido")]
    public Guid RoleId { get; init; }
}
