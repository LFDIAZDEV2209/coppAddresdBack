using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums.ProgramProgress;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.ListTemplates;

/// <summary>
/// Consulta paginada del listado de plantillas (SPEC §7.6, ERP, permiso
/// <c>Program.View</c> en la API). Filtros opcionales por texto
/// (nombre/código) y estado.
/// </summary>
public sealed record ListTemplatesQuery(
    string? Search = null,
    TemplateStatus? Status = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PaginatedTemplatesResult>;

/// <summary>Página el listado y normaliza page/pageSize (máx 100, SPEC §6).</summary>
public sealed class ListTemplatesQueryHandler(
    IProgramRepository repository) : IRequestHandler<ListTemplatesQuery, PaginatedTemplatesResult>
{
    public async Task<PaginatedTemplatesResult> Handle(ListTemplatesQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, total) = await repository.ListTemplatesAsync(
            request.Search, request.Status?.ToString(), page, pageSize, ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));

        var data = items.Select(t => new ProgramTemplateListItemDto(
            t.Id, t.Code, t.Name, t.Description, t.TotalWeeks, t.Status, t.Version,
            t.CreatedAt, t.PublishedAt)).ToList();

        return new PaginatedTemplatesResult(data, total, page, pageSize, totalPages);
    }
}