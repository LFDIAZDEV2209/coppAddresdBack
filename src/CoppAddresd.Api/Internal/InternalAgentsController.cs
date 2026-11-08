using System.Security.Cryptography;
using System.Text;
using CoppAddresd.Application.Features.Agents;
using CoppAddresd.Application.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Api.Internal;

/// <summary>Credencial servicio-a-servicio del AI Service, independiente de Telemedicina.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireAiInternalKeyAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var expected = context.HttpContext.RequestServices.GetRequiredService<IOptions<AiServiceSettings>>().Value.InternalApiKey;
        var provided = context.HttpContext.Request.Headers["X-Internal-Key"];
        if (string.IsNullOrWhiteSpace(expected) || provided.Count != 1
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(provided.ToString())))
            context.Result = new UnauthorizedObjectResult(new { message = "Clave interna inválida." });
    }
}

[ApiController]
[Route("api/v1/internal/agents")]
[AllowAnonymous]
[RequireAiInternalKey]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class InternalAgentsController(ISender sender) : ControllerBase
{
    /// <summary>Contexto clínico compacto del paciente activo para agentes internos.</summary>
    [HttpGet("patient-context")]
    [ProducesResponseType(typeof(PatientAiContextDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PatientAiContextDto>> GetPatientContext([FromQuery] Guid userId, CancellationToken ct)
    {
        var result = await sender.Send(new GetPatientAiContextQuery(userId), ct);
        return result is null ? NotFound() : Ok(result);
    }
}
