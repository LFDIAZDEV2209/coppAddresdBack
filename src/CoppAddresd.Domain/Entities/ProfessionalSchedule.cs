namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Franja horaria semanal de atención de un <see cref="Professional"/>.
/// Cada fila representa un día de la semana (weekday 1–7, ISO: lunes=1,
/// domingo=7) con su rango de horas de atención. La tabla permite hasta 7
/// filas por profesional (una por día); los días sin atención simplemente no
/// tienen fila.
/// </summary>
public sealed class ProfessionalSchedule
{
    public Guid Id { get; set; }

    public Guid ProfessionalId { get; set; }

    /// <summary>Día de la semana ISO: 1 = lunes … 7 = domingo.</summary>
    public int Weekday { get; set; }

    /// <summary>Hora de inicio de la atención (solo hora/minuto, sin zona horaria).</summary>
    public TimeOnly StartTime { get; set; }

    /// <summary>Hora de fin de la atención (solo hora/minuto, sin zona horaria).</summary>
    public TimeOnly EndTime { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Professional Professional { get; set; } = default!;
}
