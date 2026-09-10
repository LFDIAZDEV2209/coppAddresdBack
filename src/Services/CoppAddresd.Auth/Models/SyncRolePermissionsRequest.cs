using System.ComponentModel.DataAnnotations;

namespace CoppAddresd.Auth.Models;

/// <summary>
/// Request para sincronización total de permisos de un rol.
/// Un array vacío elimina todos los permisos del rol.
/// </summary>
public record SyncRolePermissionsRequest
{
    [Required(ErrorMessage = "PermissionIds es requerido")]
    public Guid[] PermissionIds { get; init; } = [];
}
