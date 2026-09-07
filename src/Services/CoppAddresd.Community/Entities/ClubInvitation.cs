namespace CoppAddresd.Community.Entities;

/// <summary>Invitación a un club por token (un solo uso, con expiración).</summary>
public sealed class ClubInvitation
{
    public Guid Id { get; set; }

    public Guid ClubId { get; set; }

    /// <summary>Perfil invitado (null = invitación genérica por token/QR).</summary>
    public Guid? ProfileId { get; set; }

    /// <summary>Token único de la invitación (para enlaces/QR).</summary>
    public string Token { get; set; } = default!;

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public Guid CreatedByProfileId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Club? Club { get; set; }
}