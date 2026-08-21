using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

public sealed class LegalDocumentRepository(AppDbContext dbContext) : ILegalDocumentRepository
{
    public async Task<IReadOnlyList<LegalDocument>> ListAsync(CancellationToken ct = default)
        => await dbContext.LegalDocuments
            .AsNoTracking()
            .Include(document => document.Versions)
            .OrderBy(document => document.Title)
            .ToListAsync(ct);

    public async Task<LegalDocument?> GetByCodeAsync(string code, CancellationToken ct = default)
        => await dbContext.LegalDocuments
            .AsNoTracking()
            .Include(document => document.Versions)
            .FirstOrDefaultAsync(document => document.Code == code, ct);

    public async Task<IReadOnlyList<LegalDocumentVersion>> ListAllVersionsAsync(CancellationToken ct = default)
        => await dbContext.LegalDocumentVersions
            .AsNoTracking()
            .Include(version => version.Document)
            .OrderByDescending(version => version.CreatedAt)
            .ToListAsync(ct);

    public async Task<LegalDocument> SaveDraftAsync(
        string code, string title, string content, string? createdBy, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var document = await dbContext.LegalDocuments
            .FirstOrDefaultAsync(item => item.Code == code, ct);

        if (document is null)
        {
            document = new LegalDocument
            {
                Id = Guid.NewGuid(),
                Code = code,
                Title = title,
                CreatedAt = now,
                UpdatedAt = now,
            };
            dbContext.LegalDocuments.Add(document);
        }
        else
        {
            document.Title = title;
            document.UpdatedAt = now;
        }

        var publishedMajor = await dbContext.LegalDocumentVersions
            .Where(version => version.DocumentId == document.Id && version.IsPublished)
            .MaxAsync(version => (int?)version.Major, ct) ?? 0;

        var lastDraftMinor = await dbContext.LegalDocumentVersions
            .Where(version => version.DocumentId == document.Id
                && version.Major == publishedMajor
                && !version.IsPublished)
            .MaxAsync(version => (int?)version.Minor, ct) ?? 0;

        var newVersion = new LegalDocumentVersion
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            Major = publishedMajor,
            Minor = lastDraftMinor + 1,
            IsPublished = false,
            Content = content,
            CreatedBy = createdBy,
            CreatedAt = now,
        };

        dbContext.LegalDocumentVersions.Add(newVersion);
        await dbContext.SaveChangesAsync(ct);
        return document;
    }

    public async Task<LegalDocument?> PublishAsync(
        string code, Guid sourceVersionId, CancellationToken ct = default)
    {
        var document = await dbContext.LegalDocuments
            .Include(item => item.Versions)
            .FirstOrDefaultAsync(item => item.Code == code, ct);
        if (document is null)
            throw new NotFoundException("Documento legal no encontrado.");

        var source = document.Versions.FirstOrDefault(version => version.Id == sourceVersionId)
            ?? throw new NotFoundException("La versión fuente no existe.");

        var lastPublishedMajor = document.Versions
            .Where(version => version.IsPublished)
            .Max(version => (int?)version.Major) ?? 0;

        var newPublished = new LegalDocumentVersion
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            Major = lastPublishedMajor + 1,
            Minor = 0,
            IsPublished = true,
            Content = source.Content,
            CreatedBy = source.CreatedBy,
            CreatedAt = DateTime.UtcNow,
        };

        document.CurrentVersionId = newPublished.Id;
        document.UpdatedAt = DateTime.UtcNow;
        dbContext.LegalDocumentVersions.Add(newPublished);
        await dbContext.SaveChangesAsync(ct);
        return document;
    }
}
