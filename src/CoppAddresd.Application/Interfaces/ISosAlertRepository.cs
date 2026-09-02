using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Repositorio de alertas SOS. Persiste el registro de cada activación
/// y actualiza los resultados por canal.
/// </summary>
public interface ISosAlertRepository
{
    /// <summary>Inserta una nueva alerta SOS y retorna la entidad persistida.</summary>
    Task<SosAlert> AddAsync(SosAlert alert, CancellationToken ct = default);

    /// <summary>Actualiza una alerta existente (status, channelResults, etc.).</summary>
    Task UpdateAsync(SosAlert alert, CancellationToken ct = default);
}
