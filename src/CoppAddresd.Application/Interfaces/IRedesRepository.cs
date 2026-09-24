using CoppAddresd.Domain.Entities.Redes;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Repositorio del módulo Acceso a Redes: catálogo de redes prestadoras,
/// radicaciones de autorizaciones y facturación RIPS.
/// </summary>
public interface IRedesRepository
{
    /// <summary>Redes habilitadas (para el validador de CUV).</summary>
    Task<IReadOnlyList<RedPrestadora>> ListRedesHabilitadasAsync(CancellationToken ct = default);

    /// <summary>Busca una red por NIT exacto (con o sin dígito de verificación).</summary>
    Task<RedPrestadora?> GetByNitAsync(string nit, CancellationToken ct = default);

    /// <summary>Siguiente número de radicado del año (para el consecutivo).</summary>
    Task<int> NextRadicacionNumberAsync(int year, CancellationToken ct = default);

    Task<IReadOnlyList<RadicacionRed>> ListRadicacionesAsync(
        string? estado, string? search, int limit = 100, CancellationToken ct = default);

    Task AddRadicacionAsync(RadicacionRed radicacion, CancellationToken ct = default);

    Task<IReadOnlyList<FacturaRed>> ListFacturasAsync(
        string? search, int limit = 100, CancellationToken ct = default);

    Task<bool> ExistsFacturaCuvAsync(string cuv, CancellationToken ct = default);

    Task AddFacturaAsync(FacturaRed factura, CancellationToken ct = default);
}
