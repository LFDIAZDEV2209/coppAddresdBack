using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Interfaces;

public interface ILegalDocumentRepository
{
    Task<IReadOnlyList<LegalDocument>> ListAsync(CancellationToken ct = default);
    Task<LegalDocument?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<LegalDocumentVersion>> ListAllVersionsAsync(CancellationToken ct = default);
    Task<LegalDocument> SaveDraftAsync(
        string code, string title, string content, string? createdBy, CancellationToken ct = default);
    Task<LegalDocument?> PublishAsync(
        string code, Guid sourceVersionId, CancellationToken ct = default);
}
