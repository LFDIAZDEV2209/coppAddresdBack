namespace CoppAddresd.Community.Entities;

/// <summary>Decisión privada y dirigida de bloquear a otro miembro.</summary>
public sealed class ProfileBlock
{
    public Guid Id { get; set; }
    public Guid BlockerProfileId { get; set; }
    public Guid BlockedProfileId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
