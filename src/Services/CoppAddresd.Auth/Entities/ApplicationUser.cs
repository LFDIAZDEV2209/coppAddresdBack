using Microsoft.AspNetCore.Identity;

namespace CoppAddresd.Auth.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Momento en que el usuario pidió eliminar su cuenta (null = no pedido).</summary>
    public DateTime? DeletionRequestedAt { get; set; }

    /// <summary>Fecha desde la cual el job de purga puede anonimizar la cuenta (solicitud + retención).</summary>
    public DateTime? PurgeAfter { get; set; }

    /// <summary>Momento en que la cuenta fue anonimizada de forma irreversible.</summary>
    public DateTime? PurgedAt { get; set; }

    public virtual ICollection<ApplicationUserRole> UserRoles { get; set; } = [];
    public virtual ICollection<UserPermission> UserPermissions { get; set; } = [];
    public virtual ICollection<UserApplication> UserApplications { get; set; } = [];
}
