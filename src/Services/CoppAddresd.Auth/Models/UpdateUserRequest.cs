using System.ComponentModel.DataAnnotations;

namespace CoppAddresd.Auth.Models;

public record UpdateUserRequest
{
    [MaxLength(100)]
    public string? FirstName { get; init; }

    [MaxLength(100)]
    public string? LastName { get; init; }

    public bool? IsActive { get; init; }

    /// <summary>
    /// Conjunto COMPLETO deseado de roles (sync total, no deltas).
    /// null = no tocar la asignación actual; lista (incluso vacía) = reemplazar
    /// por ese conjunto. Requiere Roles.Assign cuando se envía.
    /// </summary>
    public Guid[]? RoleIds { get; init; }

    /// <summary>
    /// Conjunto COMPLETO deseado de permisos directos (sync total).
    /// null = no tocar; lista (incluso vacía) = reemplazar. Requiere
    /// Permissions.Assign cuando se envía.
    /// </summary>
    public Guid[]? PermissionIds { get; init; }
}
