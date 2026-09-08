namespace CoppAddresd.Application.Features.Inventory.Events;

/// <summary>Interfaz marcadora para todos los eventos de métricas de Inventario.</summary>
public interface IInventoryMetricEvent;

/// <summary>
/// Se emite cuando se crea un documento de entrada de inventario (stock in).
/// Transporta todos los detalles de líneas necesarios para actualizar el
/// rollup sin re-consultar la base de datos.
/// </summary>
public sealed record InventoryEntryCreatedMetricEvent(
    Guid EntryId,
    DateOnly MetricDate,
    decimal TotalCost,
    IReadOnlyList<InventoryMetricLine> Lines
) : IInventoryMetricEvent;

/// <summary>
/// Se emite cuando se crea un documento de salida de inventario (stock out).
/// </summary>
public sealed record InventoryExitCreatedMetricEvent(
    Guid ExitId,
    DateOnly MetricDate,
    IReadOnlyList<InventoryMetricLine> Lines
) : IInventoryMetricEvent;

/// <summary>
/// Detalle de línea usado en los eventos de métricas (denormalizado para
/// evitar re-consultar la base de datos en el procesador de background).
/// </summary>
public sealed record InventoryMetricLine(
    string ProductName,   // Truncado a 64 chars para DimensionKey
    string Category,      // Nombre de la categoría del producto (cargada desde la entidad Product)
    int Quantity,
    decimal UnitCost
);