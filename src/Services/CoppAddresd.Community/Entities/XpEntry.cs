namespace CoppAddresd.Community.Entities;

/// <summary>Entrada de XP otorgada a un perfil (gamificación de la comunidad).</summary>
public sealed class XpEntry
{
    public Guid Id { get; set; }

    public Guid ProfileId { get; set; }

    /// <summary>Cantidad de XP (positiva).</summary>
    public int Amount { get; set; }

    /// <summary>Motivo legible (ej. "Racha destacada").</summary>
    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Profile? Profile { get; set; }
}
