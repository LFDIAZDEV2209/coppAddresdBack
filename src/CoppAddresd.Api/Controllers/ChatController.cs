using System.Security.Claims;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.DTOs.Ai;
using CoppAddresd.Application.Features.Chat;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IMediator mediator, ILogger<ChatController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// La identidad del usuario proviene SOLO del JWT autenticado
    /// (ClaimTypes.NameIdentifier); el body del cliente nunca define quién es
    /// el usuario ni el propietario de memoria/threads.
    /// </summary>
    private string? AuthenticatedUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpPost]
    public async Task<ActionResult<ChatResult>> Chat(
        [FromBody] ChatRequestDto request,
        CancellationToken ct
    )
    {
        try
        {
            var command = new ChatCommand(
                request.Message,
                request.Agent,
                request.ThreadId,
                request.AgentTypeId,
                AuthenticatedUserId
            );
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (AiServiceException ex)
        {
            _logger.LogError(
                ex,
                "AI Service rechazó el chat (status {Status}): {Detail}",
                ex.StatusCode,
                ex.Detail
            );
            return StatusCode(502, new { error = "No fue posible procesar la solicitud." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat request failed");
            return StatusCode(500, new { error = "No fue posible procesar la solicitud." });
        }
    }

    [HttpPost("stream")]
    public async Task Stream([FromBody] ChatRequestDto request, CancellationToken ct)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        try
        {
            var command = new StreamChatCommand(
                request.Message,
                request.Agent,
                request.ThreadId,
                request.AgentTypeId,
                AuthenticatedUserId
            );
            var chunks = await _mediator.Send(command, ct);

            await foreach (var chunk in chunks.WithCancellation(ct))
            {
                await Response.WriteAsync(chunk.RawData + "\n", ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Stream cancelled by client");
        }
        catch (AiServiceException ex)
        {
            _logger.LogError(
                ex,
                "AI Service rechazó el stream (status {Status}): {Detail}",
                ex.StatusCode,
                ex.Detail
            );
            var errorPayload =
                "event: error\ndata: {\"error\":\"No fue posible procesar la solicitud.\"}\n\n";
            await Response.WriteAsync(errorPayload, ct);
            await Response.Body.FlushAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stream failed");
            var errorPayload =
                "event: error\ndata: {\"error\":\"No fue posible procesar la solicitud.\"}\n\n";
            await Response.WriteAsync(errorPayload, ct);
            await Response.Body.FlushAsync(ct);
        }
    }

    /// <summary>
    /// Registra la retroalimentación del paciente sobre una respuesta del chat
    /// (rating 1-5 + comentario). La identidad (<c>user_id</c>) proviene
    /// exclusivamente del JWT; el body nunca define quién califica.
    /// Ante indisponibilidad del ai-service responde 502/503 con mensaje
    /// clínico amigable, sin filtrar trazas internas.
    /// </summary>
    [HttpPost("feedback")]
    [ProducesResponseType(typeof(ChatFeedbackResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ChatFeedbackResponseDto>> SendFeedback(
        [FromBody] ChatFeedbackRequestDto request,
        CancellationToken ct
    )
    {
        var userId = AuthenticatedUserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { message = "Usuario no identificado." });

        try
        {
            var command = new SendChatFeedbackCommand(
                request.ExecutionId,
                request.ThreadId,
                request.Rating,
                request.Comment,
                userId
            );
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (AiServiceException ex)
        {
            // El ai-service respondió error: se loguea el detalle interno pero
            // al paciente solo llega un mensaje clínico amigable (RFC 7807).
            _logger.LogError(
                ex,
                "AI Service rechazó el feedback (status {Status}): {Detail}",
                ex.StatusCode,
                ex.Detail
            );
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new ProblemDetails
                {
                    Type = "https://httpstatuses.io/502",
                    Title = "Bad Gateway",
                    Status = StatusCodes.Status502BadGateway,
                    Detail =
                        "El asistente inteligente no está disponible temporalmente. Intente en unos minutos.",
                    Instance = HttpContext.Request.Path,
                }
            );
        }
        catch (HttpRequestException ex)
        {
            // ai-service offline o red caída: 503 sin exponer el destino.
            _logger.LogError(ex, "AI Service no alcanzable al enviar feedback");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new ProblemDetails
                {
                    Type = "https://httpstatuses.io/503",
                    Title = "Service Unavailable",
                    Status = StatusCodes.Status503ServiceUnavailable,
                    Detail =
                        "El asistente inteligente no está disponible temporalmente. Intente en unos minutos.",
                    Instance = HttpContext.Request.Path,
                }
            );
        }
    }
}

public record ChatRequestDto(
    string Message,
    string? Agent = null,
    string? ThreadId = null,
    string? AgentTypeId = null
);
