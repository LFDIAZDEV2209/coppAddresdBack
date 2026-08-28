using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>
/// Implementación EF del catálogo de reglas XP (SPEC §14, B5-R) sobre
/// PostgreSQL (schema <c>app</c>). Solo persistencia y lecturas set-based;
/// la validación de rangos (multiplier &gt; 0, base &gt;= 0, ventana de
/// vigencia) vive en el validador/handler de la capa de aplicación y en las
/// CHECK constraints de la tabla.
/// </summary>
public sealed class XpRuleCatalogRepository(AppDbContext dbContext) : IXpRuleCatalogRepository
{
    public async Task<IReadOnlyList<XpRule>> ListAsync(CancellationToken ct = default)
        => await dbContext.XpRules.AsNoTracking()
            .OrderBy(r => r.Code)
            .ToListAsync(ct);

    public async Task<XpRule?> GetByCodeAsync(string code, CancellationToken ct = default)
        => await dbContext.XpRules.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Code == code, ct);

    public async Task<XpRule> UpdateAsync(XpRule rule, Guid? actorId = null, CancellationToken ct = default)
    {
        rule.UpdatedAt = DateTime.UtcNow;
        // El handler trae la entidad con AsNoTracking (GetByCodeAsync);
        // Update() la re-adjunta completa para persistir las propiedades
        // editadas sin tracking previo.
        dbContext.XpRules.Update(rule);
        await dbContext.SaveChangesAsync(ct);
        return rule;
    }
}