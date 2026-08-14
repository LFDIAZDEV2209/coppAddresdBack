using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using MediatR;

namespace CoppAddresd.Application.Features.Agents;

// --- Tipos ---

/// <summary>Lista paginada de tipos de agente con búsqueda opcional.</summary>
public record ListAgentTypesQuery(int Page, int PageSize, string? Search) : IRequest<PaginatedAgentsResult<AgentTypeDto>>;

public sealed class ListAgentTypesQueryHandler(IAgentCatalogRepository repository)
    : IRequestHandler<ListAgentTypesQuery, PaginatedAgentsResult<AgentTypeDto>>
{
    public async Task<PaginatedAgentsResult<AgentTypeDto>> Handle(ListAgentTypesQuery request, CancellationToken ct)
    {
        var total = await repository.CountAgentTypesAsync(request.Search, ct);
        var items = await repository.ListAgentTypesAsync(request.Page, request.PageSize, request.Search, ct);

        var totalPages = request.PageSize <= 0 ? 0 : (int)Math.Ceiling(total / (double)request.PageSize);

        return new PaginatedAgentsResult<AgentTypeDto>(
            items.Select(AgentTypeDto.FromEntity).ToList(),
            total,
            request.Page,
            request.PageSize,
            totalPages);
    }
}

/// <summary>Catálogo de tipos activos y disponibles (para el paciente).</summary>
public record ListActiveAgentTypesQuery : IRequest<IReadOnlyList<AgentTypeDto>>;

public sealed class ListActiveAgentTypesQueryHandler(IAgentCatalogRepository repository)
    : IRequestHandler<ListActiveAgentTypesQuery, IReadOnlyList<AgentTypeDto>>
{
    public async Task<IReadOnlyList<AgentTypeDto>> Handle(ListActiveAgentTypesQuery request, CancellationToken ct)
    {
        var all = await repository.ListAgentTypesAsync(1, 1000, null, ct);
        return all
            .Where(x => x.Status == AgentStatus.Activo)
            .Select(AgentTypeDto.FromEntity)
            .ToList();
    }
}

/// <summary>Detalle de un tipo de agente.</summary>
public record GetAgentTypeQuery(Guid Id) : IRequest<AgentTypeDto?>;

public sealed class GetAgentTypeQueryHandler(IAgentCatalogRepository repository)
    : IRequestHandler<GetAgentTypeQuery, AgentTypeDto?>
{
    public async Task<AgentTypeDto?> Handle(GetAgentTypeQuery request, CancellationToken ct)
    {
        var entity = await repository.GetAgentTypeAsync(request.Id, ct);
        return entity is null ? null : AgentTypeDto.FromEntity(entity);
    }
}

// --- Versiones ---

/// <summary>Lista las versiones de un tipo de agente.</summary>
public record ListAgentTypeVersionsQuery(Guid AgentTypeId) : IRequest<IReadOnlyList<AgentTypeVersionDto>>;

public sealed class ListAgentTypeVersionsQueryHandler(IAgentCatalogRepository repository)
    : IRequestHandler<ListAgentTypeVersionsQuery, IReadOnlyList<AgentTypeVersionDto>>
{
    public async Task<IReadOnlyList<AgentTypeVersionDto>> Handle(ListAgentTypeVersionsQuery request, CancellationToken ct)
    {
        var versions = await repository.ListVersionsAsync(request.AgentTypeId, ct);
        return versions
            .OrderByDescending(v => v.VersionNumber)
            .Select(AgentTypeVersionDto.FromEntity)
            .ToList();
    }
}

// --- Knowledge bases ---

/// <summary>Lista knowledge bases, opcionalmente filtradas por tipo de agente.</summary>
public record ListKnowledgeBasesQuery(Guid? AgentTypeId) : IRequest<IReadOnlyList<KnowledgeBaseDto>>;

public sealed class ListKnowledgeBasesQueryHandler(IAgentCatalogRepository repository)
    : IRequestHandler<ListKnowledgeBasesQuery, IReadOnlyList<KnowledgeBaseDto>>
{
    public async Task<IReadOnlyList<KnowledgeBaseDto>> Handle(ListKnowledgeBasesQuery request, CancellationToken ct)
    {
        var kbs = await repository.ListKnowledgeBasesAsync(request.AgentTypeId, ct);
        return kbs
            .OrderBy(k => k.Name)
            .Select(KnowledgeBaseDto.FromEntity)
            .ToList();
    }
}

// --- Documentos ---

/// <summary>Lista documentos de una knowledge base.</summary>
public record ListAgentDocumentsQuery(Guid KnowledgeBaseId) : IRequest<IReadOnlyList<AgentDocumentDto>>;

public sealed class ListAgentDocumentsQueryHandler(IAgentCatalogRepository repository)
    : IRequestHandler<ListAgentDocumentsQuery, IReadOnlyList<AgentDocumentDto>>
{
    public async Task<IReadOnlyList<AgentDocumentDto>> Handle(ListAgentDocumentsQuery request, CancellationToken ct)
    {
        var docs = await repository.ListDocumentsAsync(request.KnowledgeBaseId, ct);
        return docs
            .OrderByDescending(d => d.CreatedAt)
            .Select(AgentDocumentDto.FromEntity)
            .ToList();
    }
}

// --- Instancias ---

/// <summary>Lista agentes asignados a un paciente.</summary>
public record ListAgentInstancesQuery(Guid UserId) : IRequest<IReadOnlyList<AgentInstanceDto>>;

public sealed class ListAgentInstancesQueryHandler(IAgentCatalogRepository repository)
    : IRequestHandler<ListAgentInstancesQuery, IReadOnlyList<AgentInstanceDto>>
{
    public async Task<IReadOnlyList<AgentInstanceDto>> Handle(ListAgentInstancesQuery request, CancellationToken ct)
    {
        var instances = await repository.ListInstancesAsync(request.UserId, ct);
        return instances
            .OrderBy(i => i.AgentType?.Name)
            .Select(AgentInstanceDto.FromEntity)
            .ToList();
    }
}

// --- Ejecuciones (monitoreo admin, proxy del AI Service) ---

/// <summary>Lista ejecuciones con filtros (proxy del AI Service).</summary>
public record ListAgentExecutionsQuery(
    string? AgentTypeId,
    string? UserId,
    string? Status,
    DateTimeOffset? FromDate,
    DateTimeOffset? ToDate,
    int Limit,
    int Offset) : IRequest<AgentExecutionsListDto>;

public sealed class ListAgentExecutionsQueryHandler(IAgentExecutionsQueryService service)
    : IRequestHandler<ListAgentExecutionsQuery, AgentExecutionsListDto>
{
    public async Task<AgentExecutionsListDto> Handle(ListAgentExecutionsQuery request, CancellationToken ct)
    {
        var options = new AgentExecutionQueryOptions
        {
            AgentTypeId = request.AgentTypeId,
            UserId = request.UserId,
            Status = request.Status,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            Limit = request.Limit,
            Offset = request.Offset,
        };

        return await service.ListAsync(options, ct);
    }
}

/// <summary>Detalle de una ejecución (las 12 preguntas del monitoreo).</summary>
public record GetAgentExecutionQuery(string ExecutionId) : IRequest<AgentExecutionDetailDto?>;

public sealed class GetAgentExecutionQueryHandler(IAgentExecutionsQueryService service)
    : IRequestHandler<GetAgentExecutionQuery, AgentExecutionDetailDto?>
{
    public async Task<AgentExecutionDetailDto?> Handle(GetAgentExecutionQuery request, CancellationToken ct)
    {
        return await service.GetAsync(request.ExecutionId, ct);
    }
}