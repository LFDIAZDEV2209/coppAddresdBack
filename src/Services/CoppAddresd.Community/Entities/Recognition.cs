namespace CoppAddresd.Community.Entities;

/// <summary>Estado de una recognición.</summary>
public enum RecognitionStatus
{
    Pending,
    Sent,
}

/// <summary>Reconocimiento otorgado a un perfil (Miembro del mes, Racha destacada, etc.).</summary>
public sealed class Recognition
{
    public Guid Id { get; set; }

    /// <summary>Perfil destinatario del reconocimiento.</summary>
    public Guid ProfileId { get; set; }

    /// <summary>Etiqueta del tipo de reconocimiento (ej. "Miembro del mes", máx 120).</summary>
    public string TypeLabel { get; set; } = default!;

    /// <summary>Puntos de XP asociados al reconocimiento.</summary>
    public int Xp { get; set; }

    /// <summary>Estado actual: Pending o Sent.</summary>
    public RecognitionStatus Status { get; set; } = RecognitionStatus.Sent;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Perfil del admin que desencadenó el reconocimiento (nullable para seed data).</summary>
    public Guid? TriggeredByProfileId { get; set; }
}
