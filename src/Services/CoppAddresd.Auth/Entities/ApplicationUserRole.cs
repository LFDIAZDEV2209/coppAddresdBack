namespace CoppAddresd.Auth.Entities;

public class ApplicationUserRole
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }

    public virtual ApplicationUser User { get; set; } = null!;
    public virtual ApplicationRole Role { get; set; } = null!;
}
