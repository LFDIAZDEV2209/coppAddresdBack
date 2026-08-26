using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>
/// Repositorio de reglas de seguridad clínica activas, ordenadas por
/// <c>sort_order</c> (las más restrictivas primero). Lectura pura sin tracking.
/// </summary>
public sealed class SafetyRuleRepository(AppDbContext dbContext) : ISafetyRuleRepository
{
    public async Task<IReadOnlyList<PlanSafetyRule>> GetActiveAsync(CancellationToken ct = default)
        => await dbContext.PlanSafetyRules
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(ct);
}