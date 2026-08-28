using CoppAddresd.Domain.Entities.ProgramProgress;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Repositorio de lectura/escritura del catálogo de reglas XP (SPEC §14,
/// B5-R). Es un agregado separado del flujo de inscripciones: el catálogo es
/// configuración global de administración y sus endpoints (ERP) no deben
/// acoplar el contrato de <see cref="IProgramRepository"/> (ni sus fakes de
/// tests). La resolución de reglas en el otorgamiento de XP vive en
/// <c>ProgramRepository</c> (SPEC §14.3), que consulta <c>app.xp_rules</c>
/// directamente en la transacción de completación.
/// </summary>
public interface IXpRuleCatalogRepository
{
    /// <summary>
    /// Catálogo completo de reglas XP ordenado por <c>code</c> (SPEC §14.5,
    /// ERP). Es un catálogo pequeño y estable (11 reglas sembradas); se
    /// devuelve completo, sin paginación.
    /// </summary>
    Task<IReadOnlyList<XpRule>> ListAsync(CancellationToken ct = default);

    /// <summary>Regla del catálogo por código, o null si no existe (SPEC §14.5).</summary>
    Task<XpRule?> GetByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// Persiste la edición prospective de una regla (SPEC §14.4): actualiza
    /// <c>updated_at</c>/<c>updated_by</c> y guarda. Nunca reescribe el
    /// historial de <c>app.xp_ledger</c> (la columna <c>rule_code</c> es solo
    /// provenance de la entrada).
    /// </summary>
    Task<XpRule> UpdateAsync(XpRule rule, Guid? actorId = null, CancellationToken ct = default);
}