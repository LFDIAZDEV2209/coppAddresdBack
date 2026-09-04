using CoppAddresd.Application.Features.Inventory;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace CoppAddresd.Infrastructure.Repositories;

public sealed class InventoryRepository(AppDbContext dbContext) : IInventoryRepository
{
    // --- Products ---

    public async Task<Product?> GetProductByIdAsync(Guid id, CancellationToken ct = default)
        => await dbContext.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<Product?> GetProductBySkuAsync(string sku, CancellationToken ct = default)
        => await dbContext.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Sku == sku, ct);

    public async Task<(IReadOnlyList<Product> Items, int Total)> ListProductsAsync(
        string? search, string? category, string? status, int page, int pageSize, CancellationToken ct)
    {
        var query = dbContext.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.Name, pattern) ||
                EF.Functions.ILike(p.Sku, pattern) ||
                EF.Functions.ILike(p.ActiveIngredient ?? "", pattern));
        }

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category == category);

        if (!string.IsNullOrWhiteSpace(status) && status != "all")
            query = query.Where(p => p.Status == status);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<IReadOnlyList<string>> ListCategoriesAsync(CancellationToken ct = default)
        => await dbContext.Products.AsNoTracking()
            .Select(p => p.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<string>> ListSuppliersAsync(CancellationToken ct = default)
        => await dbContext.Products.AsNoTracking()
            .Where(p => p.Supplier != null)
            .Select(p => p.Supplier!)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(ct);

    public async Task<Product> AddProductAsync(Product product, CancellationToken ct = default)
    {
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(ct);
        return product;
    }

    public async Task UpdateProductAsync(Product product, CancellationToken ct = default)
    {
        dbContext.Products.Update(product);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteProductAsync(Product product, CancellationToken ct = default)
    {
        dbContext.Products.Remove(product);
        await dbContext.SaveChangesAsync(ct);
    }

    // --- Entries ---

    public async Task<InventoryEntry?> GetEntryByIdAsync(Guid id, CancellationToken ct = default)
        => await dbContext.InventoryEntries
            .AsNoTracking()
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<InventoryEntry> CreateEntryAsync(InventoryEntry entry, CancellationToken ct = default)
    {
        var products = await LoadProductsForLinesAsync(entry.Lines.Select(line => line.ProductId), ct);

        dbContext.InventoryEntries.Add(entry);
        foreach (var line in entry.Lines)
        {
            ApplyStockChange(
                products[line.ProductId], line.Quantity, entry.Reference,
                MovementDirections.Entrada, entry.Reason, entry.Responsible, line.Lot);
        }

        // Un solo SaveChanges commitea entrada + líneas + movimientos + stock de forma atómica.
        await dbContext.SaveChangesAsync(ct);
        return entry;
    }

    public async Task<IReadOnlyList<InventoryEntry>> ListEntriesAsync(int page, int pageSize, CancellationToken ct)
        => await dbContext.InventoryEntries
            .AsNoTracking()
            .Include(e => e.Lines)
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    // --- Exits ---

    public async Task<InventoryExit?> GetExitByIdAsync(Guid id, CancellationToken ct = default)
        => await dbContext.InventoryExits
            .AsNoTracking()
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<InventoryExit> CreateExitAsync(InventoryExit exit, CancellationToken ct = default)
    {
        var products = await LoadProductsForLinesAsync(exit.Lines.Select(line => line.ProductId), ct);

        foreach (var group in exit.Lines.GroupBy(line => line.ProductId))
        {
            var requested = group.Sum(line => line.Quantity);
            if (products[group.Key].Stock < requested)
            {
                throw new BusinessRuleViolationException(
                    $"No hay stock suficiente para el producto {products[group.Key].Name}. Disponible: {products[group.Key].Stock}; solicitado: {requested}.");
            }
        }

        dbContext.InventoryExits.Add(exit);
        foreach (var line in exit.Lines)
        {
            ApplyStockChange(
                products[line.ProductId], -line.Quantity, exit.Reference,
                MovementDirections.Salida, exit.Reason, exit.Responsible, line.Lot);
        }

        // Un solo SaveChanges commitea salida + líneas + movimientos + stock de forma atómica.
        await dbContext.SaveChangesAsync(ct);
        return exit;
    }

    public async Task<IReadOnlyList<InventoryExit>> ListExitsAsync(int page, int pageSize, CancellationToken ct)
        => await dbContext.InventoryExits
            .AsNoTracking()
            .Include(e => e.Lines)
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    // --- Movements ---

    public async Task<(IReadOnlyList<InventoryMovement> Items, int Total)> ListMovementsAsync(
        string? search, string? direction, DateOnly? dateFrom, DateOnly? dateTo,
        int page, int pageSize, CancellationToken ct)
    {
        var query = dbContext.InventoryMovements.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            query = query.Where(m =>
                EF.Functions.ILike(m.ProductName, pattern) ||
                EF.Functions.ILike(m.Reference ?? "", pattern) ||
                EF.Functions.ILike(m.User ?? "", pattern));
        }

        if (!string.IsNullOrWhiteSpace(direction) && direction != "all")
            query = query.Where(m => m.Direction == direction);

        if (dateFrom.HasValue)
            query = query.Where(m => m.DateTime >= dateFrom.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (dateTo.HasValue)
            query = query.Where(m => m.DateTime < dateTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(m => m.DateTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    // --- Analytics ---

    public async Task<InventoryAnalyticsDto> GetAnalyticsAsync(
        DateOnly? dateFrom = null, DateOnly? dateTo = null, CancellationToken ct = default)
    {
        // Estado actual del inventario: se calcula siempre desde Products
        // (datos puntuales del catálogo, no del rollup).
        var products = await dbContext.Products.AsNoTracking().ToListAsync(ct);

        // Rango de fechas por defecto: últimos 30 días hasta hoy.
        var to = dateTo ?? DateOnly.FromDateTime(DateTime.Today);
        var from = dateFrom ?? to.AddDays(-29);

        int entries, exits, unitsEntered, unitsExited;
        IReadOnlyList<MovementSeriesPoint> series;
        IReadOnlyList<TopMovingProduct> topMoving;
        IReadOnlyList<CategoryValue> categoryValue;

        // ── FAST PATH: lee el rollup pre-agregado en O(1) ────────────────────
        var rollupMetrics = await dbContext.InventoryDailyMetrics.AsNoTracking()
            .Where(x => x.MetricDate >= from && x.MetricDate <= to)
            .ToListAsync(ct);

        if (rollupMetrics.Count > 0)
        {
            // Métricas de totales del período (dimension "total").
            entries = (int)rollupMetrics
                .Where(x => x.MetricKey == "entries_count" && x.DimensionKey == "total")
                .Sum(x => x.TotalCount);
            exits = (int)rollupMetrics
                .Where(x => x.MetricKey == "exits_count" && x.DimensionKey == "total")
                .Sum(x => x.TotalCount);
            unitsEntered = (int)rollupMetrics
                .Where(x => x.MetricKey == "units_entered" && x.DimensionKey == "total")
                .Sum(x => x.TotalCount);
            unitsExited = (int)rollupMetrics
                .Where(x => x.MetricKey == "units_exited" && x.DimensionKey == "total")
                .Sum(x => x.TotalCount);

            // Costos del período (almacenados en centavos). El DTO no los
            // expone en Fase 1; se calculan para mantener el contrato del
            // rollup disponible para la Fase 2 del dashboard.
            var costEntered = rollupMetrics
                .Where(x => x.MetricKey == "cost_entered" && x.DimensionKey == "total")
                .Sum(x => x.TotalCount) / 100m;
            var costExited = rollupMetrics
                .Where(x => x.MetricKey == "cost_exited" && x.DimensionKey == "total")
                .Sum(x => x.TotalCount) / 100m;

            // Serie temporal diaria desde el rollup (unidades por día).
            var rangeDays = to.DayNumber - from.DayNumber + 1;
            series = Enumerable.Range(0, rangeDays).Select(i =>
            {
                var day = from.AddDays(i);
                var daysEntries = rollupMetrics
                    .Where(x => x.MetricDate == day && x.MetricKey == "units_entered" && x.DimensionKey == "total")
                    .Sum(x => (int)x.TotalCount);
                var daysExits = rollupMetrics
                    .Where(x => x.MetricDate == day && x.MetricKey == "units_exited" && x.DimensionKey == "total")
                    .Sum(x => (int)x.TotalCount);
                return new MovementSeriesPoint(day.ToString("MMM d"), daysEntries, daysExits);
            }).ToList();

            // Top moving products: unidades de entrada Y salida por producto
            // (misma semántica que el fallback OLTP, que combina ambos lados).
            topMoving = rollupMetrics
                .Where(x => x.MetricKey is "product_entries" or "product_exits")
                .GroupBy(x => x.DimensionKey)
                .Select(g => new TopMovingProduct(g.Key, (int)g.Sum(x => x.TotalCount)))
                .OrderByDescending(t => t.Quantity)
                .Take(5)
                .ToList();

            // Valor por categoría (desde rollup: category_cost en centavos).
            categoryValue = rollupMetrics
                .Where(x => x.MetricKey == "category_cost")
                .GroupBy(x => x.DimensionKey)
                .Select(g => new CategoryValue(g.Key, g.Sum(x => x.TotalCount) / 100m))
                .OrderByDescending(c => c.Value)
                .ToList();
        }
        else
        {
            // ── FALLBACK OLTP (sistema recién iniciado, sin datos en rollup) ──
            // (código original de cálculo en memoria; categoryValue alineado a la
            // semántica del rollup category_cost — ver F3 del fix de dashboards)
            var fromDate = from.ToDateTime(TimeOnly.MinValue);
            var toDate = to.AddDays(1).ToDateTime(TimeOnly.MinValue);

            // Las métricas de período se calculan sobre la fecha de la operación
            // (entradas/salidas), no sobre la marca de auditoría del movimiento,
            // para que el reporte sea coherente con las fechas seleccionadas.
            var entriesList = await dbContext.InventoryEntries.AsNoTracking()
                .Include(e => e.Lines)
                .Where(e => e.Date >= fromDate && e.Date < toDate)
                .ToListAsync(ct);
            var exitsList = await dbContext.InventoryExits.AsNoTracking()
                .Include(e => e.Lines)
                .Where(e => e.Date >= fromDate && e.Date < toDate)
                .ToListAsync(ct);

            entries = entriesList.Count;
            exits = exitsList.Count;
            unitsEntered = entriesList.Sum(e => e.Lines.Sum(l => l.Quantity));
            unitsExited = exitsList.Sum(e => e.Lines.Sum(l => l.Quantity));

            var rangeDays = to.DayNumber - from.DayNumber + 1;
            series = BuildMovementSeries(entriesList, exitsList, from, rangeDays);

            topMoving = entriesList.SelectMany(e => e.Lines)
                .Select(l => (Name: l.ProductName, Quantity: l.Quantity))
                .Concat(exitsList.SelectMany(e => e.Lines).Select(l => (Name: l.ProductName, Quantity: l.Quantity)))
                .GroupBy(m => m.Name)
                .Select(g => new TopMovingProduct(g.Key, g.Sum(m => m.Quantity)))
                .OrderByDescending(t => t.Quantity)
                .Take(5)
                .ToList();

            // Valor por categoría: costo de las líneas de ENTRADA del período
            // (misma semántica que category_cost del rollup: costo de mercancía
            // ingresada por categoría, con la misma normalización de clave).
            var productsById = products.ToDictionary(p => p.Id);
            categoryValue = entriesList.SelectMany(e => e.Lines)
                .GroupBy(l => ToCategoryDimensionKey(productsById, l.ProductId))
                .Select(g => new CategoryValue(g.Key, g.Sum(l => l.UnitCost * l.Quantity)))
                .OrderByDescending(c => c.Value)
                .ToList();
        }

        var today = DateOnly.FromDateTime(DateTime.Today);

        return new InventoryAnalyticsDto(
            TotalValue: products.Sum(p => p.Stock * p.UnitCost),
            ActiveProducts: products.Count(p => p.Status == "Activo"),
            LowStock: products.Count(p => p.Stock > 0 && p.Stock <= p.MinimumStock),
            OutOfStock: products.Count(p => p.Stock == 0),
            ExpiringSoon: products.Count(p => p.ExpirationDate.HasValue &&
                DateOnly.FromDateTime(p.ExpirationDate.Value) <= today.AddDays(90) &&
                DateOnly.FromDateTime(p.ExpirationDate.Value) > today),
            Expired: products.Count(p => p.ExpirationDate.HasValue &&
                DateOnly.FromDateTime(p.ExpirationDate.Value) < today),
            Entries: entries,
            Exits: exits,
            UnitsEntered: unitsEntered,
            UnitsExited: unitsExited,
            MovementSeries: series,
            TopMoving: topMoving,
            CategoryValue: categoryValue,
            Products: products.Select(ProductListItemDto.FromEntity).ToList());
    }

    // --- Stock adjustment ---

    private static IReadOnlyList<MovementSeriesPoint> BuildMovementSeries(
        List<InventoryEntry> entries, List<InventoryExit> exits, DateOnly from, int rangeDays)
    {
        if (rangeDays <= 31)
        {
            var points = new List<MovementSeriesPoint>();
            for (var i = 0; i < rangeDays; i++)
            {
                var day = from.AddDays(i);
                points.Add(new MovementSeriesPoint(
                    day.ToString("MMM d"),
                    entries.Where(e => DateOnly.FromDateTime(e.Date) == day).Sum(e => e.Lines.Sum(l => l.Quantity)),
                    exits.Where(e => DateOnly.FromDateTime(e.Date) == day).Sum(e => e.Lines.Sum(l => l.Quantity))));
            }
            return points;
        }

        return CombineByKey(
            entries, exits,
            rangeDays <= 183 ? (Func<DateOnly, (int Key, string Label)>)WeekKey : MonthKey);
    }

    private static IReadOnlyList<MovementSeriesPoint> CombineByKey(
        List<InventoryEntry> entries, List<InventoryExit> exits,
        Func<DateOnly, (int Key, string Label)> keySelector)
    {
        var buckets = new Dictionary<(int Key, string Label), (int In, int Out)>();
        foreach (var item in entries.SelectMany(e => e.Lines.Select(l => (Date: DateOnly.FromDateTime(e.Date), l.Quantity))))
        {
            var key = keySelector(item.Date);
            buckets.TryGetValue(key, out var value);
            buckets[key] = (value.In + item.Quantity, value.Out);
        }
        foreach (var item in exits.SelectMany(e => e.Lines.Select(l => (Date: DateOnly.FromDateTime(e.Date), l.Quantity))))
        {
            var key = keySelector(item.Date);
            buckets.TryGetValue(key, out var value);
            buckets[key] = (value.In, value.Out + item.Quantity);
        }

        return buckets
            .OrderBy(kvp => kvp.Key.Key)
            .Select(kvp => new MovementSeriesPoint(kvp.Key.Label, kvp.Value.In, kvp.Value.Out))
            .ToList();
    }

    private static (int Key, string Label) WeekKey(DateOnly date)
    {
        var dateTime = date.ToDateTime(TimeOnly.MinValue);
        var key = ISOWeek.GetYear(dateTime) * 100 + ISOWeek.GetWeekOfYear(dateTime);
        return (key, $"Semana {key % 100}");
    }

    private static (int Key, string Label) MonthKey(DateOnly date)
    {
        var key = date.Year * 100 + date.Month;
        return (key, new DateTime(date.Year, date.Month, 1).ToString("MMM yyyy"));
    }

    /// <summary>
    /// Clave de dimensión de categoría con la MISMA normalización que el rollup
    /// (category_cost): vacío → "general" y truncada a 64 chars (varchar(64)).
    /// Sin esto, una categoría larga aparece distinta según el path activo.
    /// </summary>
    private static string ToCategoryDimensionKey(Dictionary<Guid, Product> productsById, Guid productId)
    {
        var category = productsById.TryGetValue(productId, out var product) ? product.Category : null;
        if (string.IsNullOrWhiteSpace(category))
            return "general";
        return category.Length > MaxDimensionKeyLength ? category[..MaxDimensionKeyLength] : category;
    }

    private const int MaxDimensionKeyLength = 64;

    private async Task<Dictionary<Guid, Product>> LoadProductsForLinesAsync(
        IEnumerable<Guid> productIds, CancellationToken ct)
    {
        var ids = productIds.Distinct().ToList();
        var products = await dbContext.Products
            .Where(product => ids.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id, ct);

        var missingId = ids.FirstOrDefault(id => !products.ContainsKey(id));
        if (missingId != Guid.Empty)
            throw new BusinessRuleViolationException($"El producto {missingId} no existe.");

        return products;
    }

    private void ApplyStockChange(
        Product product, int delta, string reference, string direction,
        string reason, string? user, string? lot)
    {
        var before = product.Stock;
        product.Stock += delta;
        product.UpdatedAt = DateTime.UtcNow;

        dbContext.InventoryMovements.Add(new InventoryMovement
        {
            Id = Guid.NewGuid(),
            DateTime = DateTime.UtcNow,
            ProductId = product.Id,
            ProductName = product.Name,
            Direction = direction,
            Quantity = Math.Abs(delta),
            StockBefore = before,
            StockAfter = product.Stock,
            Lot = lot ?? product.Lot,
            User = user,
            Reason = reason,
            Reference = reference,
        });

    }
}
