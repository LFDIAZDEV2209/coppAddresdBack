namespace CoppAddresd.Domain.Entities;

/// <summary>Reserva de consumo append-only: borrar un medio no repone la cuota.</summary>
public sealed class ExerciseMediaUsage
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public long Bytes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
