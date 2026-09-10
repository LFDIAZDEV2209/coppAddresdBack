namespace CoppAddresd.Community.Entities;

/// <summary>Asistencia de un perfil a un evento del club (única por evento + perfil).</summary>
public sealed class EventAttendance
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public Guid ProfileId { get; set; }

    public EventAttendanceStatus Status { get; set; } = EventAttendanceStatus.Confirmado;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ClubEvent? Event { get; set; }
    public Profile? Profile { get; set; }
}