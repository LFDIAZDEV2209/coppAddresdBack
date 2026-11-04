using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Assignment;

public sealed record ListProgramAssignmentsQuery(string? Status, int Page, int PageSize, string? Search,
    string ActorScope, IReadOnlyList<Guid>? ScopedPatientIds) : IRequest<AssignmentQueuePage>;
public sealed class ListProgramAssignmentsQueryHandler(IProgramAssignmentOperationsRepository repository,
    ICacheService cache, TimeProvider clock) : IRequestHandler<ListProgramAssignmentsQuery, AssignmentQueuePage>
{
    public async Task<AssignmentQueuePage> Handle(ListProgramAssignmentsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 100);
        if (request.ScopedPatientIds is { Count: 0 }) return new([], 0, page, size, 1);
        var revision = await cache.GetAsync<AssignmentCacheRevision>("program-assignment-revision:v1", ct);
        var scope = CacheKeys.HashScope(request.ActorScope,
            request.ScopedPatientIds is null ? "global" : string.Join(",", request.ScopedPatientIds.Order()),
            request.Status, page.ToString(), size.ToString(), request.Search?.Trim(), revision?.Token);
        // El proveedor agrega erp:. La lista no se comparte entre alcances aunque tengan el mismo filtro.
        return await cache.GetOrCreateAsync(CacheKeys.Stats("program-assignments", scope), TimeSpan.FromSeconds(30),
            token => repository.ListAsync(request.Status, page, size, request.Search?.Trim(), request.ScopedPatientIds,
                clock.GetUtcNow().UtcDateTime, token), ct);
    }
}
public sealed record GetProgramAssignmentQuery(Guid Id, IReadOnlyList<Guid>? ScopedPatientIds) : IRequest<AssignmentDetailDto?>;
public sealed class GetProgramAssignmentQueryHandler(IProgramAssignmentOperationsRepository repository)
    : IRequestHandler<GetProgramAssignmentQuery, AssignmentDetailDto?>
{
    public Task<AssignmentDetailDto?> Handle(GetProgramAssignmentQuery request, CancellationToken ct) =>
        repository.GetDetailAsync(request.Id, request.ScopedPatientIds, ct);
}
