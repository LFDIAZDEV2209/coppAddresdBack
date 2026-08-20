namespace CoppAddresd.Application.Features.LegalDocuments;

public record LegalDocumentSummaryDto(
    Guid Id,
    string Code,
    string Title,
    string? CurrentVersion,
    string? LatestDraft,
    bool IsPublished,
    int VersionCount,
    DateTime? UpdatedAt);

public record LegalDocumentVersionDto(
    Guid Id,
    int Major,
    int Minor,
    string VersionLabel,
    bool IsPublished,
    string Content,
    string? CreatedBy,
    DateTime CreatedAt,
    bool IsCurrent);

public record LegalDocumentDetailDto(
    Guid Id,
    string Code,
    string Title,
    string? CurrentVersion,
    string? LatestDraft,
    bool IsPublished,
    IReadOnlyList<LegalDocumentVersionDto> Versions,
    DateTime? UpdatedAt);

public record PublishedLegalDocumentDto(
    string Code,
    string Title,
    string Content,
    string Version,
    DateTime UpdatedAt);

public record SaveDraftRequest(string Title, string Content, string? CreatedBy);

public record PublishRequest(Guid? SourceVersionId);
