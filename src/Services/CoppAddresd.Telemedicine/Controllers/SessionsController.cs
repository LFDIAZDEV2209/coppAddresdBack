using CoppAddresd.Telemedicine.Application.Constants;
using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CoppAddresd.Telemedicine.Controllers;

/// <summary>
/// Sala virtual y sesiones de una cita: join-token (acceso a la sala),
/// consulta de la sala y ciclo de vida de la sesión. La autorización del
/// participante se resuelve en el handler a partir del JWT (profesional/paciente
/// de la cita o supervisor con <c>Telemedicine.SessionsManage</c>).
/// </summary>
[ApiController]
[Route("api/v1/appointments/{appointmentId:guid}")]
[Authorize]
public class SessionsController(IMediator mediator) : ControllerBase
{
    /// <summary>Genera el token de acceso a la sala para el usuario autenticado (crea la sala si no existe).</summary>
    [HttpPost("join-token")]
    [ProducesResponseType(typeof(JoinSessionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JoinSessionResultDto>> JoinToken(Guid appointmentId, CancellationToken ct)
        => Ok(await mediator.Send(new JoinSessionCommand(appointmentId, CurrentUserId(), HasManagePermission()), ct));

    /// <summary>Detalle de la sala de la cita (estado, ventana y participantes en vivo).</summary>
    [HttpGet("room")]
    [ProducesResponseType(typeof(VirtualRoomDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VirtualRoomDto>> Room(Guid appointmentId, CancellationToken ct)
        => Ok(await mediator.Send(new GetAppointmentRoomQuery(appointmentId, CurrentUserId(), HasManagePermission()), ct));

    /// <summary>Inicia la sesión de video (solo el profesional de la cita o un supervisor).</summary>
    [HttpPost("session/start")]
    [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppointmentDto>> Start(Guid appointmentId, CancellationToken ct)
        => Ok(await mediator.Send(new StartSessionCommand(appointmentId, CurrentUserId(), HasManagePermission()), ct));

    /// <summary>Finaliza la sesión de video y completa la cita (idempotente).</summary>
    [HttpPost("session/end")]
    [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AppointmentDto>> End(
        Guid appointmentId,
        [FromBody] EndSessionDto request,
        CancellationToken ct)
        => Ok(await mediator.Send(
            new EndSessionCommand(appointmentId, request.EndReason, CurrentUserId(), HasManagePermission()), ct));

    /// <summary>Reabre una consulta completada dentro de la gracia (profesional asignado o supervisor).</summary>
    [HttpPost("session/reopen")]
    [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppointmentDto>> Reopen(Guid appointmentId, CancellationToken ct)
        => Ok(await mediator.Send(new ReopenSessionCommand(appointmentId, CurrentUserId(), HasManagePermission()), ct));

    /// <summary>
    /// Mensajes del chat de la consulta (F3) ordenados por (created_at, id), con
    /// cursor incremental <c>after</c> (ISO) + <c>afterId</c> para el polling.
    /// Misma autorización de participante/supervisor que la sala.
    /// </summary>
    [HttpGet("chat/messages")]
    [ProducesResponseType(typeof(IReadOnlyList<ChatMessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IReadOnlyList<ChatMessageDto>>> ChatMessages(
        Guid appointmentId,
        [FromQuery] DateTimeOffset? after = null,
        [FromQuery] Guid? afterId = null,
        [FromQuery] int limit = GetRoomChatMessagesQuery.DefaultLimit,
        CancellationToken ct = default)
        => Ok(await mediator.Send(
            new GetRoomChatMessagesQuery(
                appointmentId, CurrentUserId(), HasManagePermission(), after, afterId, limit), ct));

    /// <summary>
    /// Envía un mensaje al chat de la consulta (F3). El emisor y su rol se
    /// derivan del JWT; solo con la cita confirmada, en curso o completada.
    /// </summary>
    [HttpPost("chat/messages")]
    [ProducesResponseType(typeof(ChatMessageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ChatMessageDto>> SendChatMessage(
        Guid appointmentId,
        [FromBody] SendRoomChatMessageDto request,
        CancellationToken ct)
    {
        var message = await mediator.Send(
            new SendRoomChatMessageCommand(
                appointmentId, request.Body, CurrentUserId(), HasManagePermission()), ct);

        return CreatedAtAction(nameof(ChatMessages), new { appointmentId }, message);
    }

    private Guid CurrentUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }

    private bool HasManagePermission()
        => User.HasClaim("permission", AppointmentPermissionCodes.SessionsManage);
}

public sealed record EndSessionDto(string? EndReason);

public sealed record SendRoomChatMessageDto(string Body);
