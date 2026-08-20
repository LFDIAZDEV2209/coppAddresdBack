using CoppAddresd.Domain.Entities;
using System.Globalization;

namespace CoppAddresd.Infrastructure.SeedData;

/// <summary>
/// Datos demo reproducibles para que el inventario y la tienda sean visibles
/// después de ejecutar las migraciones en un entorno de desarrollo.
/// </summary>
public static class InventorySeedData
{
    public static readonly Product[] Products =
    [
        Product("10000000-0000-0000-0000-000000000001", "MED-PAR-500", "Paracetamol", "Medicamento", "Analgésicos", "Acetaminofén", "Tabletas", "500 mg", "Caja x 20", "Genfar", "Drogas La Rebaja", "PCT-DEMO-01", 248, 80, 400, "Estante A-01", "2027-10-15", 4200m, "Rotación alta."),
        Product("10000000-0000-0000-0000-000000000002", "SNK-PRO-001", "Barra de proteína cacao", "Snack saludable", "Snacks altos en proteína", null, "Barra", null, "Unidad x 50 g", "NutriFit", "Wellness Foods", "SNK-DEMO-01", 96, 20, 180, "Estante W-01", "2027-06-30", 4200m, "Sin azúcar añadida."),
        Product("10000000-0000-0000-0000-000000000003", "BEV-TEA-001", "Té frío sin azúcar", "Bebida", "Bebidas sin azúcar", null, "Botella", "500 ml", "Unidad", "Vital Drinks", "Wellness Foods", "BEV-DEMO-01", 72, 18, 140, "Nevera W-01", "2027-04-12", 3500m, "Sabor limón."),
        Product("10000000-0000-0000-0000-000000000004", "SUP-FIB-001", "Fibra soluble", "Suplemento", "Digestión y saciedad", "Psyllium", "Polvo", "300 g", "Frasco", "BioBalance", "NutriSupply", "SUP-DEMO-01", 44, 12, 80, "Estante W-02", "2028-01-20", 28500m, "Acompañar con suficiente agua."),
        Product("10000000-0000-0000-0000-000000000005", "FOO-OAT-001", "Avena integral", "Alimento saludable", "Despensa saludable", null, "Hojuelas", null, "Bolsa x 500 g", "Campo Vivo", "Healthy Market", "FOO-DEMO-01", 65, 15, 120, "Estante W-03", "2027-11-10", 6800m, "Ideal para desayunos."),
        Product("10000000-0000-0000-0000-000000000006", "DEV-SCL-001", "Balanza inteligente", "Dispositivo de salud", "Monitoreo corporal", null, "Digital", null, "Unidad", "HealthTrack", "MedTech Supply", "DEV-DEMO-01", 14, 4, 30, "Vitrina D-01", null, 118000m, "Conectividad Bluetooth."),
        Product("10000000-0000-0000-0000-000000000007", "FIT-BND-001", "Bandas elásticas de resistencia", "Equipamiento fitness", "Entrenamiento en casa", null, "Set", null, "Set x 5", "MoveWell", "Active Supply", "FIT-DEMO-01", 28, 6, 60, "Estante F-01", null, 42000m, "Tres niveles de resistencia."),
        Product("10000000-0000-0000-0000-000000000008", "CARE-SUN-001", "Protector solar SPF 50", "Cuidado personal", "Cuidado de la piel", null, "Crema", "50+", "Tubo x 120 ml", "DermaCare", "Wellness Foods", "CARE-DEMO-01", 31, 8, 70, "Estante C-01", "2027-09-05", 26000m, "Uso diario recomendado."),
        Product("10000000-0000-0000-0000-000000000009", "PHR-GLU-001", "Tiras para glucómetro", "Producto de farmacia", "Monitoreo de glucosa", null, "Tiras", null, "Caja x 50", "GlucoSafe", "MedTech Supply", "PHR-DEMO-01", 22, 8, 60, "Estante P-01", "2027-08-18", 33000m, "Compatibles con GlucoSafe."),
        Product("10000000-0000-0000-0000-000000000010", "MED-VIT-001", "Vitamina D3", "Medicamento", "Vitaminas", "Colecalciferol", "Cápsulas", "1000 UI", "Frasco x 60", "HealthLab", "NutriSupply", "MED-DEMO-01", 53, 12, 100, "Estante A-02", "2028-02-15", 18500m, "Venta libre."),
        Product("10000000-0000-0000-0000-000000000011", "INS-GLV-001", "Guantes de nitrilo", "Insumo médico", "Bioseguridad", null, "Guantes", "Talla M", "Caja x 100", "Medline", "Suministros Clínicos", "INS-DEMO-01", 84, 25, 180, "Bodega C-01", "2029-06-01", 22000m, null),
        Product("10000000-0000-0000-0000-000000000012", "OTH-BTL-001", "Botella reutilizable", "Otro", "Accesorios saludables", null, "Botella", "750 ml", "Unidad", "EcoMove", "Active Supply", "OTH-DEMO-01", 40, 10, 100, "Estante F-02", null, 28000m, "Libre de BPA.")
    ];

    public static readonly StoreItem[] StoreItems =
    [
        StoreItem("20000000-0000-0000-0000-000000000001", "10000000-0000-0000-0000-000000000002", 8900m, "Snack alto en proteína para controlar el hambre entre comidas.", true),
        StoreItem("20000000-0000-0000-0000-000000000002", "10000000-0000-0000-0000-000000000003", 7500m, "Bebida refrescante sin azúcar añadida.", true),
        StoreItem("20000000-0000-0000-0000-000000000003", "10000000-0000-0000-0000-000000000004", 49900m, "Apoyo de fibra para una alimentación equilibrada.", false),
        StoreItem("20000000-0000-0000-0000-000000000004", "10000000-0000-0000-0000-000000000005", 10900m, "Base versátil para desayunos saludables.", false),
        StoreItem("20000000-0000-0000-0000-000000000005", "10000000-0000-0000-0000-000000000006", 159000m, "Monitorea tu progreso corporal desde casa.", true)
    ];

    public static readonly InventoryEntry[] Entries =
    [
        new()
        {
            Id = Guid.Parse("30000000-0000-0000-0000-000000000001"),
            Reference = "ENT-DEMO-001",
            Date = new DateTime(2026, 8, 13),
            Reason = "Compra",
            Supplier = "Wellness Foods",
            Document = "FAC-DEMO-001",
            Responsible = "Demo seed",
            Notes = "Carga inicial del catálogo de bienestar.",
            TotalCost = 605000m,
            CreatedAt = new DateTime(2026, 8, 13, 10, 0, 0, DateTimeKind.Utc)
        },
        new()
        {
            Id = Guid.Parse("30000000-0000-0000-0000-000000000002"),
            Reference = "ENT-DEMO-002",
            Date = new DateTime(2026, 8, 16),
            Reason = "Recepción de proveedor",
            Supplier = "NutriSupply",
            Document = "REM-DEMO-002",
            Responsible = "Demo seed",
            Notes = "Reposición de suplementos y vitaminas.",
            TotalCost = 551000m,
            CreatedAt = new DateTime(2026, 8, 16, 14, 0, 0, DateTimeKind.Utc)
        }
    ];

    public static readonly InventoryEntryLine[] EntryLines =
    [
        EntryLine("31000000-0000-0000-0000-000000000001", "30000000-0000-0000-0000-000000000001", "10000000-0000-0000-0000-000000000002", "Barra de proteína cacao", 80, "SNK-DEMO-01", "2027-06-30", 4200m),
        EntryLine("31000000-0000-0000-0000-000000000002", "30000000-0000-0000-0000-000000000001", "10000000-0000-0000-0000-000000000003", "Té frío sin azúcar", 60, "BEV-DEMO-01", "2027-04-12", 3500m),
        EntryLine("31000000-0000-0000-0000-000000000003", "30000000-0000-0000-0000-000000000002", "10000000-0000-0000-0000-000000000004", "Fibra soluble", 25, "SUP-DEMO-01", "2028-01-20", 28500m)
    ];

    public static readonly InventoryExit[] Exits =
    [
        new()
        {
            Id = Guid.Parse("40000000-0000-0000-0000-000000000001"),
            Reference = "SAL-DEMO-001",
            Date = new DateTime(2026, 8, 18),
            Reason = "Venta",
            Responsible = "Demo seed",
            Notes = "Venta demo para visualizar movimientos.",
            CreatedAt = new DateTime(2026, 8, 18, 16, 0, 0, DateTimeKind.Utc)
        }
    ];

    public static readonly InventoryExitLine[] ExitLines =
    [
        ExitLine("41000000-0000-0000-0000-000000000001", "40000000-0000-0000-0000-000000000001", "10000000-0000-0000-0000-000000000002", "Barra de proteína cacao", 8, "SNK-DEMO-01", "2027-06-30", 4200m)
    ];

    public static readonly InventoryMovement[] Movements =
    [
        Movement("50000000-0000-0000-0000-000000000001", "2026-08-13T10:00:00Z", "10000000-0000-0000-0000-000000000002", "Barra de proteína cacao", "Entrada", 80, 16, 96, "SNK-DEMO-01", "Demo seed", "Compra", "ENT-DEMO-001"),
        Movement("50000000-0000-0000-0000-000000000002", "2026-08-13T10:00:00Z", "10000000-0000-0000-0000-000000000003", "Té frío sin azúcar", "Entrada", 60, 12, 72, "BEV-DEMO-01", "Demo seed", "Compra", "ENT-DEMO-001"),
        Movement("50000000-0000-0000-0000-000000000003", "2026-08-16T14:00:00Z", "10000000-0000-0000-0000-000000000004", "Fibra soluble", "Entrada", 25, 19, 44, "SUP-DEMO-01", "Demo seed", "Recepción de proveedor", "ENT-DEMO-002"),
        Movement("50000000-0000-0000-0000-000000000004", "2026-08-18T16:00:00Z", "10000000-0000-0000-0000-000000000002", "Barra de proteína cacao", "Salida", 8, 96, 88, "SNK-DEMO-01", "Demo seed", "Venta", "SAL-DEMO-001")
    ];

    private static Product Product(
        string id, string sku, string name, string productType, string category,
        string? activeIngredient, string presentation, string? concentration,
        string unit, string? manufacturer, string? supplier, string? lot,
        int stock, int minimumStock, int maximumStock, string? location,
        string? expirationDate, decimal unitCost, string? notes)
        => new()
        {
            Id = Guid.Parse(id), Sku = sku, Name = name, ProductType = productType,
            Category = category, ActiveIngredient = activeIngredient,
            Presentation = presentation, Concentration = concentration, Unit = unit,
            Manufacturer = manufacturer, Supplier = supplier, Lot = lot,
            ExpirationDate = expirationDate is null ? null : DateTime.Parse(expirationDate),
            Stock = stock, MinimumStock = minimumStock, MaximumStock = maximumStock,
            Location = location, Status = "Activo", UnitCost = unitCost, Notes = notes,
            CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)
        };

    private static StoreItem StoreItem(string id, string productId, decimal salePrice, string description, bool featured)
        => new()
        {
            Id = Guid.Parse(id), ProductId = Guid.Parse(productId), SalePrice = salePrice,
            Description = description, Featured = featured, Status = "Visible",
            CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)
        };

    private static InventoryEntryLine EntryLine(string id, string entryId, string productId, string productName, int quantity, string lot, string expirationDate, decimal unitCost)
        => new()
        {
            Id = Guid.Parse(id), EntryId = Guid.Parse(entryId), ProductId = Guid.Parse(productId),
            ProductName = productName, Quantity = quantity, Lot = lot,
            ExpirationDate = DateTime.Parse(expirationDate), UnitCost = unitCost
        };

    private static InventoryExitLine ExitLine(string id, string exitId, string productId, string productName, int quantity, string lot, string expirationDate, decimal unitCost)
        => new()
        {
            Id = Guid.Parse(id), ExitId = Guid.Parse(exitId), ProductId = Guid.Parse(productId),
            ProductName = productName, Quantity = quantity, Lot = lot,
            ExpirationDate = DateTime.Parse(expirationDate), UnitCost = unitCost
        };

    private static InventoryMovement Movement(string id, string dateTime, string productId, string productName, string direction, int quantity, int stockBefore, int stockAfter, string lot, string user, string reason, string reference)
        => new()
        {
            Id = Guid.Parse(id), DateTime = DateTime.Parse(dateTime, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal), ProductId = Guid.Parse(productId),
            ProductName = productName, Direction = direction, Quantity = quantity,
            StockBefore = stockBefore, StockAfter = stockAfter, Lot = lot, User = user,
            Reason = reason, Reference = reference
        };
}
