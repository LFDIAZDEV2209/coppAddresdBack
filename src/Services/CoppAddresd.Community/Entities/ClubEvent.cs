namespace CoppAddresd.Community.Entities;

/// <summary>Evento de un club (presencial o virtual) con cupos y lista de espera.</summary>
public sealed class ClubEvent
{
    public Guid Id { get; set; }

    public Guid ClubId { get; set; }

    public string Title { get; set; } = default!;

    public string Description { get; set; } = default!;

    public ClubEventType Type { get; set; } = ClubEventType.Virtual;

    public DateTime StartsAt { get; set; }

    public DateTime EndsAt { get; set; }

    /// <summary>Ubicación para eventos presenciales.</summary>
    public string? Location { get; set; }

    /// <summary>URL de reunión para eventos virtuales.</summary>
    public string? MeetingUrl { get; set; }

    /// <summary>Capacidad máxima de asistentes (null = sin límite).</summary>
    public int? MaxAttendees { get; set; }

    public ClubEventStatus Status { get; set; } = ClubEventStatus.Abierto;

    /// <summary>Confirmados actuales (mantenido por side-effect con anti-overbooking).</summary>
    public int ConfirmedCount { get; set; }

    /// <summary>En lista de espera actuales (mantenido por side-effect).</summary>
    public int WaitlistCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Club? Club { get; set; }

    public ICollection<EventAttendance> Attendances { get; set; } = [];
}