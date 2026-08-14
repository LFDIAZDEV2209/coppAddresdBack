using CoppAddresd.Application.Features.Agents;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

/// <summary>
/// Catálogo de agentes: tipos, versiones, knowledge bases, documentos e
/// instancias. Endpoints de administración y de asignación a pacientes.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class AgentsController(IMediator mediator) : ControllerBase
{
    // --- Tipos ---

    [HttpGet]
    public async Task<ActionResult<PaginatedAgentsResult<AgentTypeDto>>> ListAgentTypes(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new ListAgentTypesQuery(page, pageSize, search), ct);
        return Ok(result);
    }

    /// <summary>Catálogo de agentes activos disponibles (para asignar al paciente).</summary>
    [HttpGet("active")]
    public async Task<ActionResult<IReadOnlyList<AgentTypeDto>>> ListActive(
        CancellationToken ct)
    {
        var result = await mediator.Send(new ListActiveAgentTypesQuery(), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AgentTypeDto>> GetAgentType(Guid id, CancellationToken ct)
    {
        var agentType = await mediator.Send(new GetAgentTypeQuery(id), ct);
        if (agentType is null)
            return NotFound(new { message = "Tipo de agente no encontrado" });

        return Ok(agentType);
    }

    [HttpPost]
    public async Task<ActionResult<AgentTypeDto>> CreateAgentType(
        [FromBody] AgentTypeRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new CreateAgentTypeCommand(
                request.Name,
                request.Description,
                request.Specialty,
                request.IconKey,
                request.Metadata), ct);

        return CreatedAtAction(nameof(GetAgentType), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AgentTypeDto>> UpdateAgentType(
        Guid id,
        [FromBody] AgentTypeRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new UpdateAgentTypeCommand(
                id,
                request.Name,
                request.Description,
                request.Specialty,
                request.IconKey,
                request.Status,
                request.Metadata), ct);

        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAgentType(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteAgentTypeCommand(id), ct);
        return NoContent();
    }

    // --- Versiones ---

    [HttpGet("{id:guid}/versions")]
    public async Task<ActionResult<IReadOnlyList<AgentTypeVersionDto>>> ListVersions(
        Guid id,
        CancellationToken ct)
    {
        var result = await mediator.Send(new ListAgentTypeVersionsQuery(id), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/versions")]
    public async Task<ActionResult<AgentTypeVersionDto>> CreateVersion(
        Guid id,
        [FromBody] AgentTypeVersionRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new CreateAgentTypeVersionCommand(id, request.Config, request.Notes), ct);

        return CreatedAtAction(nameof(ListVersions), new { id }, result);
    }

    [HttpPost("versions/{versionId:guid}/activate")]
    public async Task<ActionResult<AgentTypeVersionDto>> ActivateVersion(
        Guid versionId,
        CancellationToken ct)
    {
        var result = await mediator.Send(new ActivateAgentTypeVersionCommand(versionId), ct);
        return Ok(result);
    }

    // --- Knowledge bases ---

    [HttpGet("knowledge-bases")]
    public async Task<ActionResult<IReadOnlyList<KnowledgeBaseDto>>> ListKnowledgeBases(
        [FromQuery] Guid? agentTypeId = null,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new ListKnowledgeBasesQuery(agentTypeId), ct);
        return Ok(result);
    }

    [HttpPost("knowledge-bases")]
    public async Task<ActionResult<KnowledgeBaseDto>> CreateKnowledgeBase(
        [FromBody] KnowledgeBaseRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new CreateKnowledgeBaseCommand(
                request.Name,
                request.Description,
                request.Scope,
                request.AgentTypeId,
                request.Status), ct);

        return CreatedAtAction(nameof(ListKnowledgeBases), new { agentTypeId = request.AgentTypeId }, result);
    }

    [HttpPut("knowledge-bases/{id:guid}")]
    public async Task<ActionResult<KnowledgeBaseDto>> UpdateKnowledgeBase(
        Guid id,
        [FromBody] KnowledgeBaseRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new UpdateKnowledgeBaseCommand(id, request.Name, request.Description, request.Status), ct);

        return Ok(result);
    }

    [HttpDelete("knowledge-bases/{id:guid}")]
    public async Task<IActionResult> DeleteKnowledgeBase(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteKnowledgeBaseCommand(id), ct);
        return NoContent();
    }

    // --- Documentos ---

    [HttpGet("knowledge-bases/{id:guid}/documents")]
    public async Task<ActionResult<IReadOnlyList<AgentDocumentDto>>> ListDocuments(
        Guid id,
        CancellationToken ct)
    {
        var result = await mediator.Send(new ListAgentDocumentsQuery(id), ct);
        return Ok(result);
    }

    [HttpPost("knowledge-bases/{id:guid}/documents")]
    public async Task<ActionResult<AgentDocumentDto>> RegisterDocument(
        Guid id,
        [FromBody] AgentDocumentRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new RegisterAgentDocumentCommand(
                id,
                request.StorageKey,
                request.FileName,
                request.ContentType,
                request.FileSizeBytes), ct);

        return CreatedAtAction(nameof(ListDocuments), new { id }, result);
    }

    [HttpDelete("documents/{id:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteAgentDocumentCommand(id), ct);
        return NoContent();
    }

    // --- Instancias ---

    /// <summary>Agentes asignados a un paciente (user_id = auth.users.Id).</summary>
    [HttpGet("instances")]
    public async Task<ActionResult<IReadOnlyList<AgentInstanceDto>>> ListInstances(
        [FromQuery] Guid userId,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new ListAgentInstancesQuery(userId), ct);
        return Ok(result);
    }

    [HttpPost("instances")]
    public async Task<ActionResult<AgentInstanceDto>> AssignInstance(
        [FromBody] AgentInstanceRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new AssignAgentInstanceCommand(request.UserId, request.AgentTypeId, request.Metadata), ct);

        return CreatedAtAction(nameof(ListInstances), new { userId = request.UserId }, result);
    }

    [HttpPut("instances/{id:guid}")]
    public async Task<ActionResult<AgentInstanceDto>> UpdateInstance(
        Guid id,
        [FromBody] AgentInstanceRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new UpdateAgentInstanceCommand(id, request.Status, request.Metadata), ct);

        return Ok(result);
    }

    [HttpDelete("instances/{id:guid}")]
    public async Task<IActionResult> DeleteInstance(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteAgentInstanceCommand(id), ct);
        return NoContent();
    }

    // --- Ejecuciones (monitoreo admin, proxy del AI Service) ---

    /// <summary>
    /// Lista ejecuciones de agentes (monitoreo). Filtros opcionales:
    /// agentTypeId, userId, status, fromDate, toDate, limit, offset.
    /// </summary>
    [HttpGet("executions")]
    public async Task<ActionResult<AgentExecutionsListDto>> ListExecutions(
        [FromQuery] string? agentTypeId = null,
        [FromQuery] string? userId = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new ListAgentExecutionsQuery(
            agentTypeId, userId, status, fromDate, toDate, limit, offset), ct);
        return Ok(result);
    }

    /// <summary>Detalle completo de una ejecución (las 12 preguntas del monitoreo).</summary>
    [HttpGet("executions/{executionId}")]
    public async Task<ActionResult<AgentExecutionDetailDto>> GetExecution(
        string executionId,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetAgentExecutionQuery(executionId), ct);
        if (result is null)
            return NotFound(new { message = "Ejecución no encontrada" });

        return Ok(result);
    }
}