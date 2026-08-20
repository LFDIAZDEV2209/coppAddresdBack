using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>
/// Persistencia de documentos clínicos (Fase 5). Solo acceso a datos; las
/// reglas de negocio viven en los handlers de Application.
/// </summary>
public sealed class DocumentRepository(AppDbContext dbContext) : IDocumentRepository
{
    private IQueryable<Document> QueryDetail()
        => dbContext.Documents
            .AsNoTracking()
            .Where(x => x.DeletedAt == null)
            .Include(x => x.Patient)
            .Include(x => x.Clinic)
            .Include(x => x.Location)
            .Include(x => x.DocumentType)
                .ThenInclude(t => t.Category);

    public async Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await QueryDetail()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<Document?> GetRootAsync(Guid rootId, CancellationToken ct = default)
        => await QueryDetail()
            .FirstOrDefaultAsync(x => x.Id == rootId && x.ParentDocumentId == null, ct);

    public async Task<(IReadOnlyList<Document> Items, int Total)> ListLatestAsync(
        Guid? patientId,
        Guid? clinicId,
        Guid? categoryId,
        Guid? documentTypeId,
        string? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        // Última versión de cada documento: fila raíz (sin parent) con el
        // conteo de versiones de su familia.
        var baseQuery = dbContext.Documents.AsNoTracking()
            .Where(x => x.DeletedAt == null && x.ParentDocumentId == null);

        if (patientId is not null)
            baseQuery = baseQuery.Where(x => x.PatientId == patientId);

        if (clinicId is not null)
            baseQuery = baseQuery.Where(x => x.ClinicId == clinicId);

        if (documentTypeId is not null)
            baseQuery = baseQuery.Where(x => x.DocumentTypeId == documentTypeId);

        if (categoryId is not null)
            baseQuery = baseQuery.Where(x => x.DocumentType.CategoryId == categoryId);

        if (!string.IsNullOrWhiteSpace(status))
            baseQuery = baseQuery.Where(x => x.Status == status);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            baseQuery = baseQuery.Where(x => EF.Functions.ILike(x.Title, pattern));
        }

        var total = await baseQuery.CountAsync(ct);

        var items = await baseQuery
            .OrderByDescending(x => x.CreatedAt)
            .Include(x => x.Patient)
            .Include(x => x.DocumentType)
                .ThenInclude(t => t.Category)
            .Include(x => x.Versions.Where(v => v.DeletedAt == null))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<IReadOnlyList<Document>> ListVersionsAsync(Guid rootId, CancellationToken ct = default)
        => await dbContext.Documents.AsNoTracking()
            .Where(x => x.DeletedAt == null
                && (x.Id == rootId || x.ParentDocumentId == rootId))
            .OrderByDescending(x => x.Version)
            .ToListAsync(ct);

    public async Task<int> GetNextVersionAsync(Guid rootId, CancellationToken ct = default)
        => await dbContext.Documents
            .Where(x => x.ParentDocumentId == rootId && x.DeletedAt == null)
            .MaxAsync(x => (int?)x.Version, ct) + 1 ?? 2;

    public async Task<Guid?> GetPatientClinicIdAsync(Guid patientId, CancellationToken ct = default)
        => await dbContext.PatientProfiles.AsNoTracking()
            .Where(x => x.Id == patientId && x.DeletedAt == null)
            .Select(x => x.ClinicId)
            .FirstOrDefaultAsync(ct);

    public async Task<bool> PatientExistsAsync(Guid patientId, CancellationToken ct = default)
        => await dbContext.PatientProfiles.AsNoTracking()
            .AnyAsync(x => x.Id == patientId && x.DeletedAt == null, ct);

    public async Task<ClinicalDocumentType?> GetTypeByIdAsync(Guid id, CancellationToken ct = default)
        => await dbContext.ClinicalDocumentTypes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<DocumentCategory>> ListCategoriesAsync(
        bool activeOnly = true,
        CancellationToken ct = default)
        => await dbContext.DocumentCategories.AsNoTracking()
            .Where(x => !activeOnly || x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ClinicalDocumentType>> ListTypesAsync(
        Guid? categoryId = null,
        bool activeOnly = true,
        CancellationToken ct = default)
    {
        var query = dbContext.ClinicalDocumentTypes.AsNoTracking()
            .Where(x => !activeOnly || x.IsActive);

        if (categoryId is not null)
            query = query.Where(x => x.CategoryId == categoryId);

        return await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Document document, CancellationToken ct = default)
    {
        dbContext.Documents.Add(document);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Document document, CancellationToken ct = default)
    {
        dbContext.Documents.Update(document);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<bool> SoftDeleteFamilyAsync(Guid rootId, Guid? deletedBy, CancellationToken ct = default)
    {
        var root = await dbContext.Documents
            .FirstOrDefaultAsync(x => x.Id == rootId && x.DeletedAt == null, ct);
        if (root is null)
            return false;

        var family = await dbContext.Documents
            .Where(x => x.ParentDocumentId == rootId && x.DeletedAt == null)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var version in family.Append(root))
        {
            version.DeletedAt = now;
            version.DeletedBy = deletedBy;
        }

        await dbContext.SaveChangesAsync(ct);
        return true;
    }
}