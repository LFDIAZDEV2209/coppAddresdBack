namespace CoppAddresd.Auth.Entities;

public class UserPreference
{
    public Guid UserId { get; set; }

    public string? Lang { get; set; }

    /// <summary>Color de acento (hex #RRGGBB) elegido por el usuario; null = default de la marca.</summary>
    public string? AccentColor { get; set; }

    /// <summary>Preferencias estéticas versionadas del avatar; no contiene datos clínicos.</summary>
    public string? AvatarConfiguration { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual ApplicationUser User { get; set; } = null!;
}
