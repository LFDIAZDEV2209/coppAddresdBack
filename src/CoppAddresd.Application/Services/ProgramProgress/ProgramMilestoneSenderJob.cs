using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Application.Services.ProgramProgress;

/// <summary>
/// Job de recordatorios proactivos de hito del programa (días 7, 14, 21, 45,
/// 60, 90): por cada inscripción activa con usuario, calcula en la zona local
/// del paciente qué hitos están en fecha (día exacto + gracia) y dentro de la
/// ventana de entrega, y notifica con la plantilla del día. Idempotente: el
/// índice único (enrollment_id, milestone_day) + el registro previo
/// (<see cref="ProgramMilestoneSend"/>) garantizan que cada hito se notifica
/// como máximo una vez. Lo consume el hosted service
/// (<c>ProgramMilestoneSenderHostedService</c>, API).
/// </summary>
public sealed class ProgramMilestoneSenderJob
{
    private readonly IProgramMilestoneRepository _repository;
    private readonly IProgramMilestoneTemplateProvider _templates;
    private readonly IProgramMilestoneNotifier _notifier;
    private readonly IOptions<ProgramMilestoneSenderSettings> _settings;
    private readonly ILogger<ProgramMilestoneSenderJob> _logger;
    private readonly Func<DateTime> _utcNow;

    /// <summary>
    /// <paramref name="utcNow"/> es un hook de testabilidad: permite fijar el
    /// instante UTC actual para que los tests de ventana de entrega sean
    /// deterministas. En producción se omite (default <see cref="DateTime.UtcNow"/>).
    /// </summary>
    public ProgramMilestoneSenderJob(
        IProgramMilestoneRepository repository,
        IProgramMilestoneTemplateProvider templates,
        IProgramMilestoneNotifier notifier,
        IOptions<ProgramMilestoneSenderSettings> settings,
        ILogger<ProgramMilestoneSenderJob> logger,
        Func<DateTime>? utcNow = null)
    {
        _repository = repository;
        _templates = templates;
        _notifier = notifier;
        _settings = settings;
        _logger = logger;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    /// <summary>
    /// Una pasada del job: recorre los candidatos activos con hito en fecha y
    /// dentro de la ventana local y notifica cada par (inscripción, día) que
    /// todavía no está en estado terminal. Idempotente: una segunda pasada no
    /// re-notifica los pares ya Sent/Skipped/Failed. Devuelve el resumen de la
    /// pasada (<see cref="ProgramMilestoneRunResult"/>) para el endpoint demo
    /// <c>POST /program-milestones/run</c>; el hosted service ignora el
    /// resultado.
    /// </summary>
    public async Task<ProgramMilestoneRunResult> RunAsync(CancellationToken ct = default)
    {
        var settings = _settings.Value;

        // Cutoff amplio: una inscripción cuyo hito 90 (fecha objetivo =
        // inicio + 89) venció hace más de 3 días de gracia ya no puede estar
        // en fecha, así que nunca entra. 95 días de margen cubren gracia +
        // tolerancia de reloj sin traer historial viejo.
        var cutoff = DateOnly.FromDateTime(_utcNow().AddDays(-95));
        var candidates = await _repository.ListActiveCandidatesAsync(cutoff, ct);
        var sent = 0;
        var skipped = 0;
        var failed = 0;
        var details = new List<ProgramMilestoneSendResult>();
        if (candidates.Count == 0)
        {
            return new ProgramMilestoneRunResult(0, sent, skipped, failed, details);
        }

        foreach (var candidate in candidates)
        {
            var localNow = ToPatientLocal(candidate.Timezone);
            var todayLocal = DateOnly.FromDateTime(localNow);

            if (!ProgramMilestoneSchedule.WithinDeliveryWindow(
                    TimeOnly.FromDateTime(localNow), settings.StartLocalHour, settings.EndLocalHour))
            {
                // Fuera de la ventana 9–21 local: la próxima pasada reintenta.
                continue;
            }

            var dueDays = ProgramMilestoneSchedule.DueMilestoneDays(
                todayLocal, candidate.StartLocalDate, settings.Days, settings.GraceDays);
            if (dueDays.Count == 0)
            {
                continue;
            }

            foreach (var day in dueDays)
            {
                var send = await HandleDayAsync(candidate, day, settings, ct);
                if (send is null || send.Status == ProgramMilestoneSendStatus.Pending)
                {
                    // null: par ya terminal o reclamación fallida (nada de esta
                    // pasada). Pending: fallo transitorio reintentable — la
                    // próxima pasada reintenta. Ninguno cuenta como resultado.
                    continue;
                }

                details.Add(ToResult(send));
                switch (send.Status)
                {
                    case ProgramMilestoneSendStatus.Sent:
                        sent++;
                        break;
                    case ProgramMilestoneSendStatus.Skipped:
                        skipped++;
                        break;
                    case ProgramMilestoneSendStatus.Failed:
                        failed++;
                        break;
                }
            }
        }

        return new ProgramMilestoneRunResult(candidates.Count, sent, skipped, failed, details);
    }

    /// <summary>
    /// Fuerza el envío del recordatorio de un hito para una inscripción
    /// (endpoints demo/herramientas <c>POST /program-milestones/force</c>):
    /// corre el MISMO pipeline de <see cref="RunAsync"/> (ventana de entrega y
    /// calendario de días NO aplican — el disparo es explícito), reutilizando
    /// el hook <c>utcNow</c> de la instancia. Idempotente: si el par
    /// (inscripción, día) ya está en estado terminal (Sent/Skipped/Failed) o
    /// agotó reintentos, devuelve su estado actual SIN re-notificar. Un día
    /// sin plantilla configurada se marca Skipped (mismo comportamiento que la
    /// pasada programada).
    /// </summary>
    /// <returns>
    /// Resultado final del par, o null si la inscripción no existe o su
    /// paciente no tiene cuenta de usuario (el controlador traduce a 404).
    /// </returns>
    public async Task<ProgramMilestoneSendResult?> ForceSendAsync(
        Guid enrollmentId,
        int milestoneDay,
        CancellationToken ct = default)
    {
        var candidate = await _repository.GetCandidateAsync(enrollmentId, ct);
        if (candidate is null)
        {
            return null;
        }

        var send = await HandleDayAsync(candidate, milestoneDay, _settings.Value, ct)
            // HandleDayAsync devuelve null cuando el par ya estaba terminal
            // (no-op): se re-lee la fila para reportar su estado actual.
            ?? await _repository.GetSendAsync(enrollmentId, milestoneDay, ct);
        if (send is null)
        {
            // La reclamación Pending falló (p. ej. pasada concurrente): no se
            // fabrica un resultado — el llamador puede reintentar.
            return null;
        }

        return ToResult(send);
    }

    /// <summary>
    /// Procesa un par (inscripción, día): reclama la fila Pending, resuelve la
    /// plantilla y notifica. Devuelve la fila con su estado final persistido,
    /// o null cuando esta invocación no hizo nada (par ya en estado terminal o
    /// con reintentos agotados, o reclamación fallida) — el llamador distingue
    /// así el trabajo real de la pasada del no-op idempotente.
    /// </summary>
    private async Task<ProgramMilestoneSend?> HandleDayAsync(
        MilestoneEnrollmentCandidate candidate,
        int day,
        ProgramMilestoneSenderSettings settings,
        CancellationToken ct)
    {
        var existing = await _repository.GetSendAsync(candidate.EnrollmentId, day, ct);

        // Reintento solo de filas no terminales: Sent/Skipped/Failed ya se
        // procesaron y una fila con Attempts >= MaxAttempts no vuelve a
        // notificarse. Las Pending con intentos disponibles se reintentan en
        // la próxima pasada (un fallo transitorio del notificador no pierde
        // el hito).
        if (existing is not null
            && (existing.Status != ProgramMilestoneSendStatus.Pending
                || existing.Attempts >= settings.MaxAttempts))
        {
            return null;
        }

        var send = existing ?? new ProgramMilestoneSend
        {
            EnrollmentId = candidate.EnrollmentId,
            MilestoneDay = day,
            Status = ProgramMilestoneSendStatus.Pending,
        };

        if (existing is null)
        {
            try
            {
                // Inserta la reclamación Pending. Cualquier fallo (p. ej.
                // violación del único por doble pasada concurrente) se trata
                // como "no fue este tick": la próxima pasada reintenta.
                await _repository.AddAsync(send, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Program.MilestoneSender: no se pudo reclamar el hito (se reintenta en la próxima pasada). enrollment={EnrollmentId} day={MilestoneDay}",
                    candidate.EnrollmentId, day);
                return null;
            }
        }

        var template = await _templates.GetAsync(day, ct);
        if (template is null)
        {
            // Plantilla no configurada para el día: misconfiguración. Se marca
            // Skipped (terminal) y se continúa con el resto de los hitos.
            send.Status = ProgramMilestoneSendStatus.Skipped;
            send.UpdatedAt = _utcNow();
            await _repository.UpdateAsync(send, ct);
            _logger.LogWarning(
                "Program.MilestoneSender: sin plantilla para el día (Skipped). enrollment={EnrollmentId} day={MilestoneDay}",
                candidate.EnrollmentId, day);
            return send;
        }

        try
        {
            var result = await _notifier.NotifyAsync(
                candidate.UserId, template.Title, template.Message, template.AgentTypeId, ct);

            send.Status = ProgramMilestoneSendStatus.Sent;
            send.ThreadId = result.ThreadId;
            send.SentAt = _utcNow();
            send.UpdatedAt = send.SentAt;
            _logger.LogInformation(
                "Program.MilestoneSent: enrollment={EnrollmentId} day={MilestoneDay} attempts={Attempts}",
                candidate.EnrollmentId, day, send.Attempts);
        }
        catch (Exception ex)
        {
            // Fallo transitorio del canal: se acumula el intento. Al alcanzar
            // MaxAttempts la fila pasa a Failed (terminal); antes, Pending
            // para que la próxima pasada reintente.
            send.Attempts += 1;
            send.Status = send.Attempts >= settings.MaxAttempts
                ? ProgramMilestoneSendStatus.Failed
                : ProgramMilestoneSendStatus.Pending;
            send.UpdatedAt = _utcNow();
            _logger.LogWarning(
                ex,
                "Program.MilestoneSender: fallo al notificar (attempts={Attempts}). enrollment={EnrollmentId} day={MilestoneDay}",
                send.Attempts, candidate.EnrollmentId, day);
        }

        await _repository.UpdateAsync(send, ct);
        return send;
    }

    private static ProgramMilestoneSendResult ToResult(ProgramMilestoneSend send)
        => new(send.EnrollmentId, send.MilestoneDay, send.Status, send.ThreadId);

    /// <summary>
    /// Hora local del paciente desde su zona IANA. Fallback a UTC si la zona
    /// no está disponible en el SO (mismas semánticas que ProgramProgressTime).
    /// </summary>
    private DateTime ToPatientLocal(string timezone)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            return TimeZoneInfo.ConvertTimeFromUtc(_utcNow(), tz);
        }
        catch (TimeZoneNotFoundException)
        {
            return _utcNow();
        }
        catch (InvalidTimeZoneException)
        {
            return _utcNow();
        }
    }
}
