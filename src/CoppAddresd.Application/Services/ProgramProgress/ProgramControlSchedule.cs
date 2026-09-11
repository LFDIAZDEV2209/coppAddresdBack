using CoppAddresd.Application.Common;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Application.Services.ProgramProgress;

/// <summary>
/// Funciones puras de calendarización de los controles del programa: qué hitos
/// están "en fecha" para una inscripción, si la hora local actual cae en la
/// ventana de entrega y (fase 2) si un control abierto está vencido para su
/// follow-up o para un cierre por temporizador. Sin I/O ni estado — unit
/// testables de forma directa. Las comparaciones de vencimiento usan el
/// instante UTC (<c>DateTimeOffset.UtcDateTime</c>): el reloj del paciente se
/// entrega ya convertido a su zona IANA por el llamador, y un cambio de horario
/// (DST) nunca desplaza el límite.
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

    /// <summary>
    /// ¿El follow-up del control está vencido y se puede enviar AHORA? True
    /// solo cuando el control sigue en <see cref="ProgramControlStatus.Sent"/>
    /// sin follow-up previo (<c>followup_sent_at</c> nulo) y pasaron
    /// <see cref="ProgramControlSettings.FollowupHours"/> desde
    /// <c>sent_at</c>, y además la hora local del paciente cae en la ventana de
    /// entrega 9–21: el follow-up es un mensaje al paciente, así que la ventana
    /// aplica (a diferencia de los cierres por temporizador,
    /// <see cref="IsMissDue"/> e <see cref="IsNoUploadTimeoutDue"/>, que actúan
    /// a cualquier hora).
    /// </summary>
    public static bool IsFollowupDue(
        ProgramControl control, DateTimeOffset patientLocalNow, ProgramControlSettings settings)
        => control.Status == ProgramControlStatus.Sent
            && control.FollowupSentAt is null
            && control.SentAt is { } sentAt
            && patientLocalNow.UtcDateTime >= sentAt.AddHours(settings.FollowupHours)
            && WithinDeliveryWindow(
                TimeOnly.FromDateTime(patientLocalNow.DateTime),
                settings.StartLocalHour,
                settings.EndLocalHour);

    /// <summary>
    /// ¿El control vencido debe cerrarse como perdido (Missed)? True cuando el
    /// control está en <see cref="ProgramControlStatus.FollowedUp"/> y pasaron
    /// <see cref="ProgramControlSettings.MissedAfterFollowupHours"/> desde
    /// <c>followup_sent_at</c>. Es un temporizador: NO aplica la ventana de
    /// entrega (el cierre es silencioso y puede ejecutarse de madrugada).
    /// </summary>
    public static bool IsMissDue(
        ProgramControl control, DateTimeOffset patientLocalNow, ProgramControlSettings settings)
        => control.Status == ProgramControlStatus.FollowedUp
            && control.FollowupSentAt is { } followupSentAt
            && patientLocalNow.UtcDateTime >= followupSentAt.AddHours(settings.MissedAfterFollowupHours);

    /// <summary>
    /// ¿El control respondido vence por no subida de examen
    /// (ClosedWithoutExam con <c>closed_reason='no_upload_timeout'</c>)? True
    /// cuando el control está en <see cref="ProgramControlStatus.Responded"/> y
    /// pasaron <see cref="ProgramControlSettings.NoUploadCloseDays"/> desde
    /// <c>responded_at</c>. Es un temporizador: NO aplica la ventana de entrega.
    /// </summary>
    public static bool IsNoUploadTimeoutDue(
        ProgramControl control, DateTimeOffset patientLocalNow, ProgramControlSettings settings)
        => control.Status == ProgramControlStatus.Responded
            && control.RespondedAt is { } respondedAt
            && patientLocalNow.UtcDateTime >= respondedAt.AddDays(settings.NoUploadCloseDays);
}