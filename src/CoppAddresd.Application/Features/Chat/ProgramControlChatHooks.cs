using CoppAddresd.Application.Common;
using CoppAddresd.Application.DTOs.Ai;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Chat;

/// <summary>
/// Hooks best-effort del flujo conversacional de controles (fase 2) para los
/// handlers de chat (<see cref="ChatCommandHandler"/> y
/// <see cref="StreamChatCommandHandler"/>): resolver el control abierto del
/// usuario, marcar Responded y construir el payload <c>control_context</c>;
/// y consumir la señal <c>control_signal</c> ("declined") que el ai-service
/// emite cuando el paciente rechazó subir el examen. TODO fallo degrada a
/// no-op — el chat JAMÁS falla por lógica de controles, y con el killswitch
/// <see cref="ProgramControlSettings.ControlsEnabled"/> apagado ninguna de
/// estas llamadas ocurre.
/// </summary>
internal static class ProgramControlChatHooks
{
    /// <summary>
    /// Resuelve el control abierto del usuario antes de reenviar el mensaje al
    /// ai-service: si está en Sent lo marca Responded (el paciente escribió en
    /// el control) y devuelve el payload de contexto + el id del control para
    /// el consumo posterior de la señal. null = sin control abierto, flag off,
    /// userId no parseable o fallo (best-effort).
    /// </summary>
    public static async Task<OpenControlContext?> TryPrepareAsync(
        IProgramControlRepository repository,
        ProgramControlSettings settings,
        string? userId,
        string? threadId,
        ILogger logger,
        CancellationToken ct)
    {
        if (!settings.ControlsEnabled)
        {
            return null;
        }

        // La identidad del usuario viene del JWT (NameIdentifier); si no es un
        // Guid no hay controles que resolver — el chat sigue sin contexto.
        if (!Guid.TryParse(userId, out var authUserId))
        {
            return null;
        }

        try
        {
            var control = await repository.FindOpenControlForUserAsync(authUserId, threadId, ct);
            if (control is null)
            {
                return null;
            }

            var status = control.Status;
            if (status == ProgramControlStatus.Sent)
            {
                var marked = await repository.MarkRespondedAsync(control.Id, DateTime.UtcNow, ct);
                if (marked)
                {
                    status = ProgramControlStatus.Responded;
                }
            }

            var payload = new ControlContextPayload(
                control.Id, control.MilestoneDay, StatusName(status), ExamPending: true);
            return new OpenControlContext(control.Id, payload);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Program.Controls: hook de chat degradado (best-effort). userId={UserId}",
                userId);
            return null;
        }
    }

    /// <summary>
    /// Consume la señal de control del ai-service tras la respuesta de chat:
    /// con <paramref name="controlSignal"/> == <c>declined</c> y un control
    /// abierto conocido, marca el cierre por rechazo explícito
    /// (ClosedWithoutExam, <c>closed_reason='declined'</c>). La transición es
    /// guardada en el repositorio: si el control ya no está en un estado de
    /// origen válido (p. ej. ya Responded y cerrado), el claim devuelve false
    /// y se ignora. Cualquier fallo se registra y no rompe la respuesta.
    /// </summary>
    public static async Task TryConsumeSignalAsync(
        IProgramControlRepository repository,
        string? controlSignal,
        Guid? controlId,
        ILogger logger,
        CancellationToken ct)
    {
        if (controlId is null || !string.Equals(controlSignal, "declined", StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            var closed = await repository.MarkClosedDeclinedAsync(controlId.Value, DateTime.UtcNow, ct);
            logger.LogInformation(
                "Program.ControlClosedDeclined: controlId={ControlId} claimed={Claimed}",
                controlId, closed);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Program.Controls: fallo al cerrar control por rechazo (best-effort). controlId={ControlId}",
                controlId);
        }
    }

    /// <summary>
    /// Nombre canónico del estado para <c>control_context.status</c> (lo que
    /// el ai-service debe parsear): sent | responded | followed_up.
    /// </summary>
    private static string StatusName(ProgramControlStatus status) => status switch
    {
        ProgramControlStatus.Sent => "sent",
        ProgramControlStatus.Responded => "responded",
        ProgramControlStatus.FollowedUp => "followed_up",
        _ => status.ToString().ToLowerInvariant(),
    };
}

/// <summary>
/// Contexto de control abierto resuelto por
/// <see cref="ProgramControlChatHooks.TryPrepareAsync"/>: id del control (para
/// el consumo posterior de la señal) + payload serializable hacia el ai-service.
/// </summary>
internal sealed record OpenControlContext(Guid ControlId, ControlContextPayload Payload);