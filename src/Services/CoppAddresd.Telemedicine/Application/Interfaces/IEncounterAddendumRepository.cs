using CoppAddresd.Telemedicine.Domain.Entities;

namespace CoppAddresd.Telemedicine.Application.Interfaces;

/// <summary>
/// Persistencia de las adendas del encuentro (F4). Append-only por contrato: la
/// única escritura es <see cref="AddAsync"/> (sin update/delete); el listado se
/// ordena por <c>(created_at, id)</c> para un orden cronológico estable.
/// </summary>
public interface IEncounterAddendumRepository
{
    /// <summary>Adendas de un encuentro ordenadas por <c>(created_at, id)</c> ascendente.</summary>
    Task<IReadOnlyList<EncounterAddendum>> ListByEncounterAsync(
        Guid encounterId,
        CancellationToken ct = default);

    /// <summary>Persiste una adenda nueva (append-only).</summary>
    Task<EncounterAddendum> AddAsync(
        EncounterAddendum addendum,
        CancellationToken ct = default);
}
