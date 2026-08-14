using System.Text;
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

    [HttpPost]
    public async Task<ActionResult<ChatResult>> Chat(
        [FromBody] ChatRequestDto request,
        CancellationToken ct)
    {
        try
        {
            var command = new ChatCommand(
                request.Message,
                request.Agent,
                request.ThreadId,
                request.AgentTypeId,
                request.UserId);
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat request failed");
            return StatusCode(500, new { error = "AI service unavailable" });
        }
    }

    [HttpPost("stream")]
    public async Task Stream(
        [FromBody] ChatRequestDto request,
        CancellationToken ct)
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
                request.UserId);
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stream failed");
            var errorPayload = $"event: error\ndata: {{\"error\":\"{ex.Message}\"}}\n\n";
            await Response.WriteAsync(errorPayload, ct);
            await Response.Body.FlushAsync(ct);
        }
    }
}

public record ChatRequestDto(
    string Message,
    string? Agent = null,
    string? ThreadId = null,
    string? AgentTypeId = null,
    string? UserId = null);
