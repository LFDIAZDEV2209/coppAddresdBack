using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

public sealed class AgentCatalogRepository(AppDbContext dbContext) : IAgentCatalogRepository
{
    // --- Tipos ---

    public async Task<AgentType?> GetAgentTypeAsync(Guid id, CancellationToken ct = default)
        => await dbContext.AgentTypes
            .AsNoTracking()
            .Include(x => x.ActiveVersion)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<AgentType>> ListAgentTypesAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken ct = default)
    {
        IQueryable<AgentType> query = dbContext.AgentTypes.AsNoTracking().Include(x => x.ActiveVersion);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.Name, pattern) ||
                EF.Functions.ILike(x.Specialty ?? string.Empty, pattern) ||
                EF.Functions.ILike(x.Description ?? string.Empty, pattern));
        }

        return await query
            .OrderBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<int> CountAgentTypesAsync(string? search, CancellationToken ct = default)
    {
        var query = dbContext.AgentTypes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.Name, pattern) ||
                EF.Functions.ILike(x.Specialty ?? string.Empty, pattern) ||
                EF.Functions.ILike(x.Description ?? string.Empty, pattern));
        }

        return await query.CountAsync(ct);
    }

    public async Task<AgentType> AddAgentTypeAsync(AgentType agentType, CancellationToken ct = default)
    {
        await dbContext.AgentTypes.AddAsync(agentType, ct);
        await dbContext.SaveChangesAsync(ct);
        return agentType;
    }

    public async Task UpdateAgentTypeAsync(AgentType agentType, CancellationToken ct = default)
    {
        dbContext.AgentTypes.Update(agentType);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteAgentTypeAsync(AgentType agentType, CancellationToken ct = default)
    {
        dbContext.AgentTypes.Remove(agentType);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<bool> AgentTypeExistsAsync(Guid id, CancellationToken ct = default)
        => await dbContext.AgentTypes.AsNoTracking().AnyAsync(x => x.Id == id, ct);

    public async Task<bool> AgentTypeHasInstancesAsync(Guid id, CancellationToken ct = default)
        => await dbContext.AgentInstances.AsNoTracking().AnyAsync(x => x.AgentTypeId == id, ct);

    public async Task<bool> AgentTypeNameExistsAsync(string name, Guid? excludeId, CancellationToken ct = default)
        => await dbContext.AgentTypes.AsNoTracking().AnyAsync(x =>
            x.Name == name && (!excludeId.HasValue || x.Id != excludeId.Value), ct);

    // --- Versiones ---

    public async Task<AgentTypeVersion?> GetVersionAsync(Guid id, CancellationToken ct = default)
        => await dbContext.AgentTypeVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<AgentTypeVersion>> ListVersionsAsync(Guid agentTypeId, CancellationToken ct = default)
        => await dbContext.AgentTypeVersions
            .AsNoTracking()
            .Where(x => x.AgentTypeId == agentTypeId)
            .OrderBy(x => x.VersionNumber)
            .ToListAsync(ct);

    public async Task<int> NextVersionNumberAsync(Guid agentTypeId, CancellationToken ct = default)
    {
        var max = await dbContext.AgentTypeVersions
            .AsNoTracking()
            .Where(x => x.AgentTypeId == agentTypeId)
            .MaxAsync(x => (int?)x.VersionNumber, ct);

        return (max ?? 0) + 1;
    }

    public async Task<AgentTypeVersion> AddVersionAsync(AgentTypeVersion version, CancellationToken ct = default)
    {
        await dbContext.AgentTypeVersions.AddAsync(version, ct);
        await dbContext.SaveChangesAsync(ct);
        return version;
    }

    public async Task UpdateVersionAsync(AgentTypeVersion version, CancellationToken ct = default)
    {
        dbContext.AgentTypeVersions.Update(version);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<AgentTypeVersion?> GetActiveVersionAsync(Guid agentTypeId, CancellationToken ct = default)
        => await dbContext.AgentTypeVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AgentTypeId == agentTypeId && x.IsActive, ct);

    // --- Knowledge bases ---

    public async Task<KnowledgeBase?> GetKnowledgeBaseAsync(Guid id, CancellationToken ct = default)
        => await dbContext.KnowledgeBases
            .AsNoTracking()
            .Include(x => x.Documents)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<KnowledgeBase>> ListKnowledgeBasesAsync(Guid? agentTypeId, CancellationToken ct = default)
    {
        IQueryable<KnowledgeBase> query = dbContext.KnowledgeBases.AsNoTracking().Include(x => x.Documents);

        if (agentTypeId is not null)
            query = query.Where(x => x.AgentTypeId == agentTypeId);

        return await query.ToListAsync(ct);
    }

    public async Task<KnowledgeBase> AddKnowledgeBaseAsync(KnowledgeBase kb, CancellationToken ct = default)
    {
        await dbContext.KnowledgeBases.AddAsync(kb, ct);
        await dbContext.SaveChangesAsync(ct);
        return kb;
    }

    public async Task UpdateKnowledgeBaseAsync(KnowledgeBase kb, CancellationToken ct = default)
    {
        dbContext.KnowledgeBases.Update(kb);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteKnowledgeBaseAsync(KnowledgeBase kb, CancellationToken ct = default)
    {
        dbContext.KnowledgeBases.Remove(kb);
        await dbContext.SaveChangesAsync(ct);
    }

    // --- Documentos ---

    public async Task<AgentDocument?> GetDocumentAsync(Guid id, CancellationToken ct = default)
        => await dbContext.AgentDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<AgentDocument>> ListDocumentsAsync(Guid knowledgeBaseId, CancellationToken ct = default)
        => await dbContext.AgentDocuments
            .AsNoTracking()
            .Where(x => x.KnowledgeBaseId == knowledgeBaseId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task<AgentDocument> AddDocumentAsync(AgentDocument document, CancellationToken ct = default)
    {
        await dbContext.AgentDocuments.AddAsync(document, ct);
        await dbContext.SaveChangesAsync(ct);
        return document;
    }

    public async Task DeleteDocumentAsync(AgentDocument document, CancellationToken ct = default)
    {
        dbContext.AgentDocuments.Remove(document);
        await dbContext.SaveChangesAsync(ct);
    }

    // --- Instancias ---

    public async Task<AgentInstance?> GetInstanceAsync(Guid id, CancellationToken ct = default)
        => await dbContext.AgentInstances
            .AsNoTracking()
            .Include(x => x.AgentType)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<AgentInstance>> ListInstancesAsync(Guid userId, CancellationToken ct = default)
        => await dbContext.AgentInstances
            .AsNoTracking()
            .Include(x => x.AgentType)
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AgentInstance>> ListInstancesByAgentTypeAsync(Guid agentTypeId, CancellationToken ct = default)
        => await dbContext.AgentInstances
            .AsNoTracking()
            .Where(x => x.AgentTypeId == agentTypeId)
            .ToListAsync(ct);

    public async Task<AgentInstance> AddInstanceAsync(AgentInstance instance, CancellationToken ct = default)
    {
        await dbContext.AgentInstances.AddAsync(instance, ct);
        await dbContext.SaveChangesAsync(ct);
        return instance;
    }

    public async Task UpdateInstanceAsync(AgentInstance instance, CancellationToken ct = default)
    {
        dbContext.AgentInstances.Update(instance);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteInstanceAsync(AgentInstance instance, CancellationToken ct = default)
    {
        dbContext.AgentInstances.Remove(instance);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<bool> InstanceExistsForUserAndTypeAsync(
        Guid userId,
        Guid agentTypeId,
        Guid? excludeId,
        CancellationToken ct = default)
        => await dbContext.AgentInstances.AsNoTracking().AnyAsync(x =>
            x.UserId == userId &&
            x.AgentTypeId == agentTypeId &&
            (!excludeId.HasValue || x.Id != excludeId.Value), ct);
}