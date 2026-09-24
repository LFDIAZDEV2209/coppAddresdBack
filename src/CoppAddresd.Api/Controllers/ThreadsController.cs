using System.Security.Claims;
using CoppAddresd.Application.Features.Threads;
using CoppAddresd.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

/// <summary>
/// Lectura (proxy) del estado de un thread del paciente desde el AI Service.
/// El frontend NUNCA llama al AI Service directo: este controlador hace de
/// puente y la X-Internal-Key vive solo en el backend.
/// </summary>
[ApiController]
[Route("api/v1/threads")]
[Authorize]
public class ThreadsController(IAiServiceClient aiService, ILogger<ThreadsController> logger)
    : ControllerBase
{
    /// <summary>
    /// Devuelve el historial de un thread (messageCount + lastMessage +
    /// messages en orden) que el paciente verá al abrir el chat tras un
    /// re-login. <c>messages</c> es aditivo: viaja la página solicitada
    /// (tope 100 visibles); un ai-service anterior no lo envía y viaja como
    /// lista vacía.
    /// Paginación desde el más reciente: <c>limit</c> (default 10 en el AI
    /// Service, 1..100) y <c>before</c> (offset desde el final del historial,
    /// devuelto como <c>nextCursor</c>); la respuesta incluye <c>hasMore</c> y
    /// <c>nextCursor</c>. Sin valor, se delegan los defaults del AI Service.
    /// Blindaje anti-IDOR: el dueño del thread proviene estricta y
    /// exclusivamente del JWT (<c>ClaimTypes.NameIdentifier</c>). El query
    /// param <c>userId</c> se conserva solo por compatibilidad y se IGNORA
    /// siempre: nunca define identidad ni alcance.
    /// Si el AI Service falla o el thread no existe se devuelve un estado
    /// vacío (nunca 500) para que el front degrade a chat vacío.
    /// </summary>
    [HttpGet("{threadId}/messages")]
    [ProducesResponseType(typeof(ThreadStateResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ThreadStateResult>> GetMessages(
        string threadId,
        [FromQuery] string? userId,
        [FromQuery] int? limit,
        [FromQuery] int? before,
        CancellationToken ct
    )
    {
        // Anti-IDOR: el query param userId se ignora deliberadamente
        // (retenido solo por compatibilidad); la identidad sale solo del JWT.
        _ = userId;
        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(ownerId))
            return Unauthorized(new { message = "Usuario no identificado." });

        try
        {
            var state = await aiService.GetThreadStateAsync(threadId, ownerId, limit, before, ct);
            return Ok(state);
        }
        catch (Exception ex)
        {
            // Degradación: el AI Service puede no estar disponible o el thread
            // puede no existir todavía (404). El cliente siempre recibe 200.
            logger.LogWarning(
                ex,
                "No se pudo leer el thread {ThreadId} en el AI Service",
                threadId
            );
            return Ok(new ThreadStateResult(threadId, 0, null, []));
        }
    }
}
