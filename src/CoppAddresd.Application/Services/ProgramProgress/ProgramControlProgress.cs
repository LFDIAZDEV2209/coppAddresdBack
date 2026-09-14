using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Application.Services.ProgramProgress;

/// <summary>
/// Read model puro de los Controles del programa para el ERP (UC-004): arma la
/// línea de tiempo de hitos (días configurados en
/// <see cref="Common.ProgramControlSettings.Days"/>), el control abierto
/// vigente, el próximo vencimiento y los contadores de adherencia a partir de
/// las filas de <c>app.program_controls</c> de una inscripción. Sin I/O ni
/// estado — unit testable de forma directa (mismo estilo que
/// <see cref="ProgramControlSchedule"/>).
/// </summary>
public static class ProgramControlProgress
{
    /// <summary>
    /// Construye el read model de una inscripción:
    /// <list type="bullet">
    /// <item><c>milestones</c>: una entrada por día configurado (ascendente,
    /// sin duplicados); un día sin fila queda Pending con campos nulos.</item>
    /// <item><c>current_control</c>: el control ABIERTO (Sent/Responded/
    /// FollowedUp) más reciente por <c>SentAt</c>; null si no hay ninguno.</item>
    /// <item><c>next_due</c>: el menor día configurado sin fila de control;
    /// null si todos los días ya tienen fila. La fecha objetivo es
    /// <c>startLocalDate + (día - 1)</c> (día 1 = fecha de inicio).</item>
    /// <item><c>adherence</c>: contadores de estado, respuestas, follow-ups y
    /// mensajes enviados sobre TODAS las filas recibidas.</item>
    /// </list>
    /// </summary>
    /// <param name="controls">
    /// Filas de <c>program_controls</c> de la inscripción (cualquier orden; el
    /// índice único por (inscripción, día) garantiza una fila por hito).
    /// </param>
    /// <param name="milestoneDays">Días configurados del programa.</param>
    /// <param name="startLocalDate">Fecha local de inicio de la inscripción.</param>
    /// <param name="utcNow">
    /// Instante UTC actual. El contrato definido de UC-004 no depende del reloj
    /// (los estados y vencimientos se derivan de las filas y del calendario);
    /// se recibe como punto único de verdad temporal para las reglas futuras
    /// del módulo.
    /// </param>
    public static ProgramControlProgressSnapshot Build(
        IReadOnlyList<ProgramControl> controls,
        IReadOnlyList<int> milestoneDays,
        DateOnly startLocalDate,
        DateTime utcNow)
    {
        // Días normalizados: sin negativos ni duplicados y ascendentes (el
        // orden del read model no depende del orden de configuración).
        var days = milestoneDays.Where(d => d > 0).Distinct().OrderBy(d => d).ToList();

        // Una fila por día: ante datos legados duplicados se conserva la más
        // reciente por CreatedAt.
        var controlsByDay = new Dictionary<int, ProgramControl>();
        foreach (var control in controls.OrderBy(c => c.CreatedAt))
        {
            controlsByDay[control.MilestoneDay] = control;
        }

        var milestones = days
            .Select(day =>
            {
                var targetDate = startLocalDate.AddDays(day - 1);
                if (!controlsByDay.TryGetValue(day, out var control))
                {
                    return new ProgramControlMilestoneEntry(
                        day,
                        targetDate,
                        StatusName(ProgramControlStatus.Pending),
                        ControlId: null,
                        SentAt: null,
                        RespondedAt: null,
                        FollowupSentAt: null,
                        CompletedAt: null,
                        ClosedReason: null);
                }

                return new ProgramControlMilestoneEntry(
                    day,
                    targetDate,
                    StatusName(control.Status),
                    control.Id,
                    control.SentAt,
                    control.RespondedAt,
                    control.FollowupSentAt,
                    control.CompletedAt,
                    control.ClosedReason);
            })
            .ToList();

        var current = controls
            .Where(IsOpen)
            .OrderByDescending(c => c.SentAt)
            .FirstOrDefault();

        var nextDay = days.FirstOrDefault(day => !controlsByDay.ContainsKey(day));
        var nextDue =
            nextDay == 0 ? null : new ProgramControlNextDue(nextDay, startLocalDate.AddDays(nextDay - 1));

        var adherence = new ProgramControlAdherenceCounts(
            Completed: controls.Count(c => c.Status == ProgramControlStatus.Completed),
            Missed: controls.Count(c => c.Status == ProgramControlStatus.Missed),
            ClosedWithoutExam: controls.Count(c => c.Status == ProgramControlStatus.ClosedWithoutExam),
            Pending: days.Count(day => !controlsByDay.ContainsKey(day)),
            Responded: controls.Count(c => c.Status == ProgramControlStatus.Responded),
            FollowupsSent: controls.Count(c => c.FollowupSentAt is not null),
            MessagesSent: controls.Count(c => c.SentAt is not null));

        return new ProgramControlProgressSnapshot(milestones, current is null ? null : ToCurrent(current), nextDue, adherence);
    }

    /// <summary>
    /// Control ABIERTO = no terminal con ciclo conversacional en curso:
    /// Sent (esperando respuesta), Responded (esperando examen o cierre) y
    /// FollowedUp (seguimiento enviado). Failed/Skipped/Missed/Completed/
    /// ClosedWithoutExam son terminales.
    /// </summary>
    public static bool IsOpen(ProgramControl control) =>
        control.Status
            is ProgramControlStatus.Sent
                or ProgramControlStatus.Responded
                or ProgramControlStatus.FollowedUp;

    /// <summary>
    /// Nombre de contrato del estado (snake_case) expuesto por el read model
    /// ERP; estable ante renames del enum.
    /// </summary>
    public static string StatusName(ProgramControlStatus status) =>
        status switch
        {
            ProgramControlStatus.Pending => "pending",
            ProgramControlStatus.Sent => "sent",
            ProgramControlStatus.Responded => "responded",
            ProgramControlStatus.FollowedUp => "followed_up",
            ProgramControlStatus.Completed => "completed",
            ProgramControlStatus.ClosedWithoutExam => "closed_without_exam",
            ProgramControlStatus.Missed => "missed",
            ProgramControlStatus.Failed => "failed",
            ProgramControlStatus.Skipped => "skipped",
            _ => status.ToString().ToLowerInvariant(),
        };

    private static ProgramControlCurrent ToCurrent(ProgramControl control) =>
        new(control.Id, control.MilestoneDay, StatusName(control.Status), control.SentAt);
}

/// <summary>Entrada de la línea de tiempo: un día de hito configurado.</summary>
public sealed record ProgramControlMilestoneEntry(
    int MilestoneDay,
    DateOnly TargetDate,
    string Status,
    Guid? ControlId,
    DateTime? SentAt,
    DateTime? RespondedAt,
    DateTime? FollowupSentAt,
    DateTime? CompletedAt,
    string? ClosedReason);

/// <summary>Control abierto vigente (el más reciente por <c>SentAt</c>).</summary>
public sealed record ProgramControlCurrent(
    Guid ControlId,
    int MilestoneDay,
    string Status,
    DateTime? SentAt);

/// <summary>Próximo hito por cumplir: menor día configurado sin fila de control.</summary>
public sealed record ProgramControlNextDue(int MilestoneDay, DateOnly TargetDate);

/// <summary>Contadores de adherencia del ciclo de controles de la inscripción.</summary>
public sealed record ProgramControlAdherenceCounts(
    int Completed,
    int Missed,
    int ClosedWithoutExam,
    int Pending,
    int Responded,
    int FollowupsSent,
    int MessagesSent);

/// <summary>Read model puro de los Controles de una inscripción (UC-004).</summary>
public sealed record ProgramControlProgressSnapshot(
    IReadOnlyList<ProgramControlMilestoneEntry> Milestones,
    ProgramControlCurrent? CurrentControl,
    ProgramControlNextDue? NextDue,
    ProgramControlAdherenceCounts Adherence);
