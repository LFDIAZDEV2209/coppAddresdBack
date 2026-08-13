using Microsoft.AspNetCore.Identity;

namespace CoppAddresd.Auth.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<ApplicationUserRole> UserRoles { get; set; } = [];
    public virtual ICollection<UserPermission> UserPermissions { get; set; } = [];
    public virtual ICollection<UserApplication> UserApplications { get; set; } = [];
}
