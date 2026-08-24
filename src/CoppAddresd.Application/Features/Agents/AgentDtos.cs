using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;

namespace CoppAddresd.Application.Features.Agents;

/// <summary>DTO de tipo de agente.</summary>
public record AgentTypeDto(
    Guid Id,
    string Name,
    string? Description,
    string? Specialty,
    string? IconKey,
    string? Slug,
    AgentStatus Status,
    string Metadata,
    Guid? ActiveVersionId,
    int? ActiveVersionNumber,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt)
{
    public static AgentTypeDto FromEntity(AgentType entity) => new(
        entity.Id,
        entity.Name,
        entity.Description,
        entity.Specialty,
        entity.IconKey,
        entity.Slug,
        entity.Status,
        entity.Metadata,
        entity.ActiveVersionId,
        entity.ActiveVersion?.VersionNumber,
        entity.CreatedAt,
        entity.UpdatedAt);
}

/// <summary>DTO de versión de tipo de agente.</summary>
public record AgentTypeVersionDto(
    Guid Id,
    Guid AgentTypeId,
    int VersionNumber,
    bool IsActive,
    string Config,
    string? Notes,
    DateTimeOffset CreatedAt)
{
    public static AgentTypeVersionDto FromEntity(AgentTypeVersion entity) => new(
        entity.Id,
        entity.AgentTypeId,
        entity.VersionNumber,
        entity.IsActive,
        entity.Config,
        entity.Notes,
        entity.CreatedAt);
}

/// <summary>DTO de knowledge base.</summary>
public record KnowledgeBaseDto(
    Guid Id,
    string Name,
    string? Description,
    KnowledgeBaseScope Scope,
    Guid? AgentTypeId,
    AgentStatus Status,
    int DocumentCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt)
{
    public static KnowledgeBaseDto FromEntity(KnowledgeBase entity) => new(
        entity.Id,
        entity.Name,
        entity.Description,
        entity.Scope,
        entity.AgentTypeId,
        entity.Status,
        entity.Documents.Count,
        entity.CreatedAt,
        entity.UpdatedAt);
}

/// <summary>DTO de documento de conocimiento.</summary>
public record AgentDocumentDto(
    Guid Id,
    Guid KnowledgeBaseId,
    string StorageKey,
    string FileName,
    string? ContentType,
    long? FileSizeBytes,
    AgentDocumentStatus Status,
    string? ErrorMessage,
    int? ChunksCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt)
{
    public static AgentDocumentDto FromEntity(AgentDocument entity) => new(
        entity.Id,
        entity.KnowledgeBaseId,
        entity.StorageKey,
        entity.FileName,
        entity.ContentType,
        entity.FileSizeBytes,
        entity.Status,
        entity.ErrorMessage,
        entity.ChunksCount,
        entity.CreatedAt,
        entity.UpdatedAt);
}

/// <summary>DTO de instancia de agente asignada a un paciente.</summary>
public record AgentInstanceDto(
    Guid Id,
    Guid UserId,
    Guid AgentTypeId,
    string? AgentTypeName,
    AgentInstanceStatus Status,
    string Metadata,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt)
{
    public static AgentInstanceDto FromEntity(AgentInstance entity) => new(
        entity.Id,
        entity.UserId,
        entity.AgentTypeId,
        entity.AgentType?.Name,
        entity.Status,
        entity.Metadata,
        entity.CreatedAt,
        entity.UpdatedAt);
}

/// <summary>Resultado paginado de listados del módulo de agentes.</summary>
public record PaginatedAgentsResult<T>(
    IReadOnlyList<T> Data,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);

/// <summary>Payload de creación/actualización de tipo de agente.</summary>
public record AgentTypeRequest(
    string Name,
    string? Description,
    string? Specialty,
    string? IconKey,
    string? Slug,
    string? Status,
    string? Metadata);

/// <summary>Payload de creación de versión de agente.</summary>
public record AgentTypeVersionRequest(
    string Config,
    string? Notes);

/// <summary>Payload de creación/actualización de knowledge base.</summary>
public record KnowledgeBaseRequest(
    string Name,
    string? Description,
    string Scope,
    Guid? AgentTypeId,
    string? Status);

/// <summary>Payload de registro de documento (el archivo ya vive en storage).</summary>
public record AgentDocumentRequest(
    string StorageKey,
    string FileName,
    string? ContentType,
    long? FileSizeBytes);

/// <summary>Payload de asignación/actualización de instancia de agente.</summary>
public record AgentInstanceRequest(
    Guid UserId,
    Guid AgentTypeId,
    string? Status,
    string? Metadata);