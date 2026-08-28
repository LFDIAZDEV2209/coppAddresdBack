namespace CoppAddresd.Auth.Entities;

public class UserPreference
{
    public Guid UserId { get; set; }

    public string? Lang { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual ApplicationUser User { get; set; } = null!;
}
