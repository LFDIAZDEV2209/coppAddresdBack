using System.ComponentModel.DataAnnotations;

namespace CoppAddresd.Auth.Models;

public record AssignPermissionToUserRequest
{
    [Required(ErrorMessage = "PermissionId es requerido")]
    public Guid PermissionId { get; init; }
}
