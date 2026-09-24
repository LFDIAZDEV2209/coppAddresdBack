using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.Redes;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>
/// Repositorio EF del módulo Acceso a Redes (catálogo de redes prestadoras,
/// radicaciones de autorizaciones y facturación RIPS en el schema erp).
/// </summary>
public sealed class RedesRepository(AppDbContext dbContext) : IRedesRepository
{
    public async Task<IReadOnlyList<RedPrestadora>> ListRedesHabilitadasAsync(
        CancellationToken ct = default)
        => await dbContext.RedesPrestadoras
            .AsNoTracking()
            .Where(x => x.IsActive && x.Habilitacion == "habilitado")
            .OrderBy(x => x.RazonSocial)
            .ToListAsync(ct);

    public async Task<RedPrestadora?> GetByNitAsync(
        string nit, CancellationToken ct = default)
    {
        var normalized = NormalizeNit(nit);
        if (normalized.Length == 0) return null;

        var baseNit = normalized.Split('-')[0];
        return await dbContext.RedesPrestadoras
            .AsNoTracking()
            .Where(x =>
                x.Nit == normalized ||
                EF.Functions.ILike(x.Nit, baseNit + "%"))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int> NextRadicacionNumberAsync(
        int year, CancellationToken ct = default)
    {
        var prefix = $"RAD-{year}-";
        var max = await dbContext.RadicacionesRed
            .AsNoTracking()
            .Where(x => EF.Functions.ILike(x.Consecutivo, prefix + "%"))
            .Select(x => x.Consecutivo)
            .ToListAsync(ct);

        var last = max
            .Select(x => int.TryParse(x[prefix.Length..], out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
        return last + 1;
    }

    public async Task<IReadOnlyList<RadicacionRed>> ListRadicacionesAsync(
        string? estado, string? search, int limit = 100, CancellationToken ct = default)
    {
        var query = dbContext.RadicacionesRed
            .AsNoTracking()
            .Include(x => x.Lineas)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(estado))
        {
            query = query.Where(x => x.Estado == estado);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.Consecutivo, pattern) ||
                EF.Functions.ILike(x.RazonSocial, pattern) ||
                EF.Functions.ILike(x.Nit, pattern) ||
                EF.Functions.ILike(x.DiagnosticoCie10, pattern));
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Consecutivo)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task AddRadicacionAsync(
        RadicacionRed radicacion, CancellationToken ct = default)
    {
        await dbContext.RadicacionesRed.AddAsync(radicacion, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<FacturaRed>> ListFacturasAsync(
        string? search, int limit = 100, CancellationToken ct = default)
    {
        var query = dbContext.FacturasRed
            .AsNoTracking()
            .Include(x => x.Archivos)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.Cuv, pattern) ||
                EF.Functions.ILike(x.FacturaNumero, pattern) ||
                EF.Functions.ILike(x.PrestadorRazonSocial, pattern) ||
                EF.Functions.ILike(x.PrestadorNit, pattern));
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.FechaRadicacion)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsFacturaCuvAsync(
        string cuv, CancellationToken ct = default)
        => await dbContext.FacturasRed
            .AsNoTracking()
            .AnyAsync(x => x.Cuv == cuv.Trim(), ct);

    public async Task AddFacturaAsync(
        FacturaRed factura, CancellationToken ct = default)
    {
        await dbContext.FacturasRed.AddAsync(factura, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    /// <summary>Normaliza el NIT: recorta y quita espacios internos.</summary>
    private static string NormalizeNit(string nit)
        => string.Join(string.Empty, nit.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
