using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Documents;

/// <summary>
/// Lista paginada de documentos (última versión de cada documento) con
/// filtros por paciente, clínica, categoría, tipo, estado y búsqueda por
/// título. La frontera de clínica la impone el controlador (contexto activo).
/// </summary>
public record ListDocumentsQuery(
    Guid? PatientId = null,
    Guid? ClinicId = null,
    Guid? CategoryId = null,
    Guid? DocumentTypeId = null,
    string? Status = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20)
    : IRequest<PaginatedDocumentsResult>;

public sealed class ListDocumentsQueryHandler(
    IDocumentRepository repository) : IRequestHandler<ListDocumentsQuery, PaginatedDocumentsResult>
{
    public async Task<PaginatedDocumentsResult> Handle(ListDocumentsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();

        var (items, total) = await repository.ListLatestAsync(
            request.PatientId,
            request.ClinicId,
            request.CategoryId,
            request.DocumentTypeId,
            request.Status,
            search,
            page,
            pageSize,
            ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));

        return new PaginatedDocumentsResult(
            items.Select(i => DocumentListItemDto.FromEntity(i, i.Versions.Count)).ToList(),
            total,
            page,
            pageSize,
            totalPages);
    }
}