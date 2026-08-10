namespace CoppAddresd.Auth.Entities;

public class UserPermission
{
    public Guid UserId { get; set; }
    public Guid PermissionId { get; set; }

    public virtual ApplicationUser User { get; set; } = null!;
    public virtual Permission Permission { get; set; } = null!;
}
