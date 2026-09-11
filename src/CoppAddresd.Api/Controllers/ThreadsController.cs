using System.Security.Claims;
using CoppAddresd.Application.Features.Threads;
using CoppAddresd.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

/// <summary>
/// Lectura (proxy) del estado de un thread del paciente desde el AI Service.
/// El frontend NUNCA llama al AI Service directo: este controlador hace de
/// puente y la X-Internal-Key vive solo en el backend.
/// </summary>
[ApiController]
[Route("api/v1/threads")]
public class ThreadsController(
    IAiServiceClient aiService,
    ILogger<ThreadsController> logger) : ControllerBase
{
    /// <summary>
    /// Devuelve el historial de un thread (messageCount + lastMessage +
    /// messages en orden) que el paciente verá al abrir el chat tras un
    /// re-login. <c>messages</c> es aditivo (el AI Service lo limita a los
    /// últimos 100 visibles); un ai-service anterior no lo envía y viaja como
    /// lista vacía.
    /// El dueño del thread se deriva del JWT cuando hay sesión real; el query
    /// param <c>userId</c> es SOLO el respaldo del flujo demo (login aún no
    /// conectado) — cuando el login real exista, el JWT siempre gana.
    /// Si el AI Service falla o el thread no existe se devuelve un estado
    /// vacío (nunca 500) para que el front degrade a chat vacío.
    /// </summary>
    [HttpGet("{threadId}/messages")]
    [ProducesResponseType(typeof(ThreadStateResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ThreadStateResult>> GetMessages(
        string threadId,
        [FromQuery] string? userId,
        CancellationToken ct)
    {
        var ownerId = ResolveOwnerId(userId);
        if (ownerId is null)
            return Unauthorized(new { message = "Usuario no identificado." });

        try
        {
            var state = await aiService.GetThreadStateAsync(threadId, ownerId, ct);
            return Ok(state);
        }
        catch (Exception ex)
        {
            // Degradación: el AI Service puede no estar disponible o el thread
            // puede no existir todavía (404). El cliente siempre recibe 200.
            logger.LogWarning(ex, "No se pudo leer el thread {ThreadId} en el AI Service", threadId);
            return Ok(new ThreadStateResult(threadId, 0, null, []));
        }
    }

    /// <summary>
    /// Identidad del dueño del thread: JWT primero (regla de seguridad del
    /// backend), query param solo como respaldo mientras el login demo no
    /// emite JWT válidos.
    /// </summary>
    private string? ResolveOwnerId(string? queryUserId)
    {
        var jwtUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(jwtUserId))
            return jwtUserId;

        return string.IsNullOrWhiteSpace(queryUserId) ? null : queryUserId;
    }
}