namespace CoppAddresd.Application.Features.Inventory;

// --- Products ---

public record ProductDto(
    Guid Id, string Sku, string Name, string ProductType, string Category,
    string? ActiveIngredient, string Presentation, string? Concentration,
    string Unit, string? Manufacturer, string? Supplier, string? Lot,
    DateTime? ExpirationDate, int Stock, int MinimumStock, int MaximumStock,
    string? Location, string Status, decimal UnitCost, string? Notes,
    DateTime CreatedAt, DateTime? UpdatedAt)
{
    public static ProductDto FromEntity(Domain.Entities.Product p) => new(
        p.Id, p.Sku, p.Name, p.ProductType, p.Category,
        p.ActiveIngredient, p.Presentation, p.Concentration,
        p.Unit, p.Manufacturer, p.Supplier, p.Lot,
        p.ExpirationDate, p.Stock, p.MinimumStock, p.MaximumStock,
        p.Location, p.Status, p.UnitCost, p.Notes,
        p.CreatedAt, p.UpdatedAt);
}

public record ProductListItemDto(
    Guid Id, string Sku, string Name, string ProductType, string Category,
    string? ActiveIngredient, string Presentation, string? Concentration,
    string Unit, string? Supplier, string? Manufacturer, string? Lot,
    DateTime? ExpirationDate, int Stock, int MinimumStock, int MaximumStock,
    string? Location, string Status, decimal UnitCost, string? Notes)
{
    public static ProductListItemDto FromEntity(Domain.Entities.Product p) => new(
        p.Id, p.Sku, p.Name, p.ProductType, p.Category,
        p.ActiveIngredient, p.Presentation, p.Concentration,
        p.Unit, p.Supplier, p.Manufacturer, p.Lot,
        p.ExpirationDate, p.Stock, p.MinimumStock, p.MaximumStock,
        p.Location, p.Status, p.UnitCost, p.Notes);
}

public record CreateProductRequest(
    string Sku, string Name, string ProductType, string Category,
    string? ActiveIngredient, string Presentation, string? Concentration,
    string Unit, string? Manufacturer, string? Supplier, string? Lot,
    DateTime? ExpirationDate, int Stock, int MinimumStock, int MaximumStock,
    string? Location, string Status, decimal UnitCost, string? Notes);

public record UpdateProductRequest(
    string Sku, string Name, string ProductType, string Category,
    string? ActiveIngredient, string Presentation, string? Concentration,
    string Unit, string? Manufacturer, string? Supplier, string? Lot,
    DateTime? ExpirationDate, int Stock, int MinimumStock, int MaximumStock,
    string? Location, string Status, decimal UnitCost, string? Notes);

public record PaginatedProductsResult(
    IReadOnlyList<ProductListItemDto> Data, int Total, int Page, int PageSize, int TotalPages);

// --- Entries ---

public record InventoryEntryDto(
    Guid Id, string Reference, DateTime Date, string Reason,
    string? Supplier, string? Document, string? Responsible,
    string? Notes, decimal TotalCost, DateTime CreatedAt,
    IReadOnlyList<InventoryEntryLineDto> Lines)
{
    public static InventoryEntryDto FromEntity(Domain.Entities.InventoryEntry e) => new(
        e.Id, e.Reference, e.Date, e.Reason,
        e.Supplier, e.Document, e.Responsible,
        e.Notes, e.TotalCost, e.CreatedAt,
        e.Lines.Select(InventoryEntryLineDto.FromEntity).ToList());
}

public record InventoryEntryLineDto(
    Guid Id, Guid ProductId, string ProductName, int Quantity,
    string? Lot, DateTime? ExpirationDate, decimal UnitCost)
{
    public static InventoryEntryLineDto FromEntity(Domain.Entities.InventoryEntryLine l) => new(
        l.Id, l.ProductId, l.ProductName, l.Quantity,
        l.Lot, l.ExpirationDate, l.UnitCost);
}

public record CreateEntryRequest(
    DateTime Date, string Reason, string? Supplier, string? Document,
    string? Responsible, string? Notes,
    IReadOnlyList<InventoryEntryLineInput> Lines);

public record InventoryEntryLineInput(
    Guid ProductId, string ProductName, int Quantity,
    string? Lot, DateTime? ExpirationDate, decimal UnitCost);

// --- Exits ---

public record InventoryExitDto(
    Guid Id, string Reference, DateTime Date, string Reason,
    string? Responsible, string? PatientName, string? Notes,
    DateTime CreatedAt, IReadOnlyList<InventoryExitLineDto> Lines)
{
    public static InventoryExitDto FromEntity(Domain.Entities.InventoryExit e) => new(
        e.Id, e.Reference, e.Date, e.Reason,
        e.Responsible, e.PatientName, e.Notes,
        e.CreatedAt,
        e.Lines.Select(InventoryExitLineDto.FromEntity).ToList());
}

public record InventoryExitLineDto(
    Guid Id, Guid ProductId, string ProductName, int Quantity,
    string? Lot, DateTime? ExpirationDate, decimal UnitCost)
{
    public static InventoryExitLineDto FromEntity(Domain.Entities.InventoryExitLine l) => new(
        l.Id, l.ProductId, l.ProductName, l.Quantity,
        l.Lot, l.ExpirationDate, l.UnitCost);
}

public record CreateExitRequest(
    DateTime Date, string Reason, string? Responsible,
    string? PatientName, string? Notes,
    IReadOnlyList<InventoryExitLineInput> Lines);

public record InventoryExitLineInput(
    Guid ProductId, string ProductName, int Quantity,
    string? Lot, DateTime? ExpirationDate, decimal UnitCost);

// --- Movements ---

public record InventoryMovementDto(
    Guid Id, DateTime DateTime, Guid ProductId, string ProductName,
    string Direction, int Quantity, int StockBefore, int StockAfter,
    string? Lot, string? User, string? Reason, string? Reference)
{
    public static InventoryMovementDto FromEntity(Domain.Entities.InventoryMovement m) => new(
        m.Id, m.DateTime, m.ProductId, m.ProductName,
        m.Direction, m.Quantity, m.StockBefore, m.StockAfter,
        m.Lot, m.User, m.Reason, m.Reference);
}

// --- Analytics ---

public record InventoryAnalyticsDto(
    decimal TotalValue, int ActiveProducts, int LowStock, int OutOfStock,
    int ExpiringSoon, int Expired, int Entries, int Exits,
    int UnitsEntered, int UnitsExited,
    IReadOnlyList<MovementSeriesPoint> MovementSeries,
    IReadOnlyList<TopMovingProduct> TopMoving,
    IReadOnlyList<CategoryValue> CategoryValue,
    IReadOnlyList<ProductListItemDto> Products);

public record MovementSeriesPoint(string Label, int Entries, int Exits);
public record TopMovingProduct(string Name, int Quantity);
public record CategoryValue(string Category, decimal Value);

// --- Paginated results ---

public record PaginatedEntriesResult(
    IReadOnlyList<InventoryEntryDto> Data, int Total, int Page, int PageSize, int TotalPages);

public record PaginatedExitsResult(
    IReadOnlyList<InventoryExitDto> Data, int Total, int Page, int PageSize, int TotalPages);

public record PaginatedMovementsResult(
    IReadOnlyList<InventoryMovementDto> Data, int Total, int Page, int PageSize, int TotalPages);
