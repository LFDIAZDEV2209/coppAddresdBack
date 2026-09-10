namespace CoppAddresd.Community.Entities;

/// <summary>Membresía de un perfil en un club (PK compuesta club + perfil).</summary>
public sealed class ClubMember
{
    public Guid ClubId { get; set; }

    public Guid ProfileId { get; set; }

    public ClubMemberRole Role { get; set; } = ClubMemberRole.Miembro;

    public ClubMemberStatus Status { get; set; } = ClubMemberStatus.Activo;

    /// <summary>Hasta cuándo está silenciado el miembro (null si no está silenciado).</summary>
    public DateTime? MutedUntil { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Club? Club { get; set; }
    public Profile? Profile { get; set; }
}