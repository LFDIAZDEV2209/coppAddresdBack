namespace CoppAddresd.Application.Services.ProgramProgress;

/// <summary>
/// Funciones puras de calendarización de los controles del programa: qué hitos
/// están "en fecha" para una inscripción y si la hora local actual cae en la
/// ventana de entrega. Sin I/O ni estado — unit testables de forma directa.
/// </summary>
public static class ProgramControlSchedule
{
    /// <summary>
    /// Días de hito pendientes de una inscripción: para cada hito N, la fecha
    /// objetivo es <c>startLocalDate + (N - 1)</c> (día 1 = fecha de inicio) y
    /// está en fecha si <paramref name="todayLocal"/> cae entre esa fecha y la
    /// misma más <paramref name="graceDays"/> días de gracia. Devuelve los días
    /// en orden ascendente, sin duplicados (el orden del job no depende del
    /// orden de configuración).
    /// </summary>
    public static IReadOnlyList<int> DueMilestoneDays(
        DateOnly todayLocal,
        DateOnly startLocalDate,
        IEnumerable<int> milestoneDays,
        int graceDays)
    {
        var due = new List<int>();

        foreach (var day in milestoneDays.Where(d => d > 0).Distinct().OrderBy(d => d))
        {
            var target = startLocalDate.AddDays(day - 1);
            var graceEnd = target.AddDays(graceDays);
            if (todayLocal >= target && todayLocal <= graceEnd)
            {
                due.Add(day);
            }
        }

        return due;
    }

    /// <summary>
    /// ¿La hora local cae en la ventana de entrega? La hora de inicio es
    /// inclusiva (<c>localTime.Hour &gt;= startHour</c>) y la de fin exclusiva
    /// (<c>localTime.Hour &lt; endHour</c>): con 9–21 la ventana cubre 09:00 a
    /// 20:59 y el recordatorio nunca llega de madrugada.
    /// </summary>
    public static bool WithinDeliveryWindow(TimeOnly localTime, int startHour, int endHour)
        => localTime.Hour >= startHour && localTime.Hour < endHour;
}