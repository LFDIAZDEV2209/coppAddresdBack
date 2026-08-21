using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using MediatR;

namespace CoppAddresd.Application.Features.LegalDocuments;

public record ListLegalDocumentsQuery : IRequest<IReadOnlyList<LegalDocumentSummaryDto>>;

public sealed class ListLegalDocumentsQueryHandler(ILegalDocumentRepository repository)
    : IRequestHandler<ListLegalDocumentsQuery, IReadOnlyList<LegalDocumentSummaryDto>>
{
    public async Task<IReadOnlyList<LegalDocumentSummaryDto>> Handle(
        ListLegalDocumentsQuery _, CancellationToken ct)
        => (await repository.ListAsync(ct)).Select(ToSummary).ToList();

    internal static LegalDocumentSummaryDto ToSummary(LegalDocument document)
    {
        var current = document.Versions.FirstOrDefault(version => version.Id == document.CurrentVersionId);
        var latestDraft = document.Versions
            .Where(version => !version.IsPublished)
            .OrderByDescending(version => version.Major)
            .ThenByDescending(version => version.Minor)
            .FirstOrDefault();
        return new(
            document.Id,
            document.Code,
            document.Title,
            current?.VersionLabel,
            latestDraft?.VersionLabel,
            current is not null,
            document.Versions.Count,
            document.UpdatedAt ?? document.CreatedAt);
    }
}

public record ListAllLegalDocumentVersionsQuery : IRequest<IReadOnlyList<LegalDocumentVersionListDto>>;

public sealed class ListAllLegalDocumentVersionsQueryHandler(ILegalDocumentRepository repository)
    : IRequestHandler<ListAllLegalDocumentVersionsQuery, IReadOnlyList<LegalDocumentVersionListDto>>
{
    public async Task<IReadOnlyList<LegalDocumentVersionListDto>> Handle(
        ListAllLegalDocumentVersionsQuery _, CancellationToken ct)
    {
        var versions = await repository.ListAllVersionsAsync(ct);
        return versions.Select(version => new LegalDocumentVersionListDto(
            version.DocumentId,
            version.Document.Code,
            version.Document.Title,
            version.Id,
            version.Major,
            version.Minor,
            version.VersionLabel,
            version.IsPublished,
            version.Content,
            version.CreatedBy,
            version.CreatedAt,
            version.Id == version.Document.CurrentVersionId)).ToList();
    }
}

public record GetLegalDocumentQuery(string Code) : IRequest<LegalDocumentDetailDto?>;

public sealed class GetLegalDocumentQueryHandler(ILegalDocumentRepository repository)
    : IRequestHandler<GetLegalDocumentQuery, LegalDocumentDetailDto?>
{
    public async Task<LegalDocumentDetailDto?> Handle(GetLegalDocumentQuery request, CancellationToken ct)
    {
        var document = await repository.GetByCodeAsync(request.Code, ct);
        return document is null ? null : ToDetail(document);
    }

    internal static LegalDocumentDetailDto ToDetail(LegalDocument document)
    {
        var versions = document.Versions
            .OrderByDescending(version => version.Major)
            .ThenByDescending(version => version.Minor)
            .Select(version => new LegalDocumentVersionDto(
                version.Id,
                version.Major,
                version.Minor,
                version.VersionLabel,
                version.IsPublished,
                version.Content,
                version.CreatedBy,
                version.CreatedAt,
                version.Id == document.CurrentVersionId))
            .ToList();

        var current = versions.FirstOrDefault(version => version.IsCurrent);
        var latestDraft = versions.FirstOrDefault(version => !version.IsPublished);
        return new(
            document.Id,
            document.Code,
            document.Title,
            current?.VersionLabel,
            latestDraft?.VersionLabel,
            current is not null,
            versions,
            document.UpdatedAt ?? document.CreatedAt);
    }
}

public record ListLegalDocumentVersionsQuery(string Code) : IRequest<IReadOnlyList<LegalDocumentVersionDto>>;

public sealed class ListLegalDocumentVersionsQueryHandler(ILegalDocumentRepository repository)
    : IRequestHandler<ListLegalDocumentVersionsQuery, IReadOnlyList<LegalDocumentVersionDto>>
{
    public async Task<IReadOnlyList<LegalDocumentVersionDto>> Handle(
        ListLegalDocumentVersionsQuery request, CancellationToken ct)
    {
        var document = await repository.GetByCodeAsync(request.Code, ct)
            ?? throw new NotFoundException("Documento legal no encontrado.");
        return document.Versions
            .OrderByDescending(version => version.Major)
            .ThenByDescending(version => version.Minor)
            .Select(version => new LegalDocumentVersionDto(
                version.Id,
                version.Major,
                version.Minor,
                version.VersionLabel,
                version.IsPublished,
                version.Content,
                version.CreatedBy,
                version.CreatedAt,
                version.Id == document.CurrentVersionId))
            .ToList();
    }
}

public record SaveDraftCommand(string Code, SaveDraftRequest Request) : IRequest<LegalDocumentDetailDto>;

public sealed class SaveDraftCommandHandler(ILegalDocumentRepository repository)
    : IRequestHandler<SaveDraftCommand, LegalDocumentDetailDto>
{
    public async Task<LegalDocumentDetailDto> Handle(SaveDraftCommand request, CancellationToken ct)
    {
        var document = await repository.SaveDraftAsync(
            request.Code,
            request.Request.Title,
            request.Request.Content,
            request.Request.CreatedBy,
            ct);
        var saved = await repository.GetByCodeAsync(document.Code, ct)
            ?? throw new InvalidOperationException("No se pudo recuperar el documento guardado.");
        return GetLegalDocumentQueryHandler.ToDetail(saved);
    }
}

public record PublishCommand(string Code, PublishRequest Request) : IRequest<LegalDocumentDetailDto>;

public sealed class PublishCommandHandler(ILegalDocumentRepository repository)
    : IRequestHandler<PublishCommand, LegalDocumentDetailDto>
{
    public async Task<LegalDocumentDetailDto> Handle(PublishCommand request, CancellationToken ct)
    {
        var document = await repository.GetByCodeAsync(request.Code, ct)
            ?? throw new NotFoundException("Documento legal no encontrado.");

        var sourceId = request.Request.SourceVersionId
            ?? document.Versions
                .Where(version => !version.IsPublished)
                .OrderByDescending(version => version.Major)
                .ThenByDescending(version => version.Minor)
                .Select(version => version.Id)
                .FirstOrDefault();

        if (sourceId == Guid.Empty)
            throw new NotFoundException("No hay ninguna versión para publicar.");

        var published = await repository.PublishAsync(request.Code, sourceId, ct)
            ?? throw new NotFoundException("Documento legal no encontrado.");
        var saved = await repository.GetByCodeAsync(published.Code, ct)
            ?? throw new InvalidOperationException("No se pudo recuperar el documento publicado.");
        return GetLegalDocumentQueryHandler.ToDetail(saved);
    }
}
