using CoppAddresd.Application.Features.ProgramProgress.DTOs.ActivityLog;
using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.ListProgramActivityLog;

/// <summary>
/// Bitácora de actividad del módulo programa para el ERP: entradas del log de
/// auditoría trigger-based (<c>audit.activity_logs</c>) de las tablas
/// <c>app.*</c> del módulo, paginada (default 20, orden descendente por
/// <c>occurred_at</c>) con filtros opcionales por tabla, acción
/// (INSERT/UPDATE/DELETE), ventana de fechas y actor (email contiene).
///
/// La API la protege con <c>Program.View</c>. La lectura NUNCA escribe y no
/// expone <c>old_data</c>/<c>new_data</c> (posible PHI, convención §8.5).
/// </summary>
public sealed record ListProgramActivityLogQuery(
    int Page = 1,
    int PageSize = 20,
    string? TableName = null,
    string? Action = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? Actor = null)
    : IRequest<PaginatedActivityLogResult>;

public sealed class ListProgramActivityLogQueryHandler(IProgramRepository repository)
    : IRequestHandler<ListProgramActivityLogQuery, PaginatedActivityLogResult>
{
    public Task<PaginatedActivityLogResult> Handle(
        ListProgramActivityLogQuery request, CancellationToken ct)
        => repository.ListActivityLogAsync(
            request.Page,
            request.PageSize,
            request.TableName,
            request.Action,
            request.From,
            request.To,
            request.Actor,
            ct);
}
