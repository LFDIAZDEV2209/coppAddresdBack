using System.ComponentModel.DataAnnotations;

namespace CoppAddresd.Auth.Models;

public record AssignPermissionToRoleRequest
{
    [Required(ErrorMessage = "PermissionId es requerido")]
    public Guid PermissionId { get; init; }
}
