using System.Linq.Expressions;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;

namespace CoppAddresd.Application.Services;

/// <summary>Política común para accesos directos, recursos indirectos y consultas de listas.</summary>
public sealed class PatientScopeGuard(IPatientScopeActor actor, IPatientScopeStore store) : IPatientScopeGuard
{
    public async Task EnsureNutritionPlansAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var requested = ids.Distinct().ToArray();
        if (requested.Length == 0) return;
        var visible = await VisibleAsync("Nutrition.View", ct);
        if (await store.CountVisibleNutritionPlansAsync(requested, visible, ct) != requested.Length) throw Missing();
    }

    public Task EnsureAsync(Guid patientId, CancellationToken ct)
        => EnsureAsync(patientId, "Program.Profile360View", ct);

    public async Task EnsureAsync(Guid patientId, string permission, CancellationToken ct)
    {
        var visible = await VisibleAsync(permission, ct);
        if (!await store.ContainsAsync(visible, patientId, ct)) throw Missing();
    }

    public async Task<Guid> EnsureResourceAsync(PatientResource resource, Guid id, string permission, CancellationToken ct)
    {
        // Comprobar capacidad antes de resolver el recurso evita lecturas clínicas sin permiso.
        var visible = await VisibleAsync(permission, ct);
        var owner = await store.OwnerAsync(resource, id, ct);
        if (owner is not { } patient || !await store.ContainsAsync(visible, patient, ct)) throw Missing();
        return patient;
    }

    public async Task<IQueryable<T>> FilterAsync<T>(IQueryable<T> query, Expression<Func<T, Guid>> patientId,
        string permission, CancellationToken ct)
    {
        var visible = await VisibleAsync(permission, ct);
        var contains = Expression.Call(typeof(Queryable), nameof(Queryable.Contains), [typeof(Guid)],
            visible.Expression, patientId.Body);
        return query.Where(Expression.Lambda<Func<T, bool>>(contains, patientId.Parameters));
    }

    public async Task<PatientScopeFilter> GetFilterAsync(string permission, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (actor.UserId is null || string.IsNullOrWhiteSpace(permission)
            || !await actor.HasPermissionAsync(permission, ct))
            throw new ForbiddenException("Se requiere permiso para esta operación.");
        return new(actor.IsAdmin, actor.IsAdmin ? null : await actor.ProfessionalIdAsync(ct));
    }

    private async Task<IQueryable<Guid>> VisibleAsync(string permission, CancellationToken ct)
    {
        var filter = await GetFilterAsync(permission, ct);
        return store.VisiblePatients(filter.ProfessionalId, filter.IsAdmin);
    }

    private static NotFoundException Missing() => new("Recurso no encontrado.");
}
