using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedWellnessInventoryCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "inventory",
                table: "inventory_entries",
                columns: new[] { "id", "created_at", "date", "document", "notes", "reason", "reference", "responsible", "supplier", "total_cost" },
                values: new object[,]
                {
                    { new Guid("30000000-0000-0000-0000-000000000001"), new DateTime(2026, 8, 13, 10, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 8, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), "FAC-DEMO-001", "Carga inicial del catálogo de bienestar.", "Compra", "ENT-DEMO-001", "Demo seed", "Wellness Foods", 605000m },
                    { new Guid("30000000-0000-0000-0000-000000000002"), new DateTime(2026, 8, 16, 14, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 8, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), "REM-DEMO-002", "Reposición de suplementos y vitaminas.", "Recepción de proveedor", "ENT-DEMO-002", "Demo seed", "NutriSupply", 551000m }
                });

            migrationBuilder.InsertData(
                schema: "inventory",
                table: "inventory_exits",
                columns: new[] { "id", "created_at", "date", "notes", "patient_name", "reason", "reference", "responsible" },
                values: new object[] { new Guid("40000000-0000-0000-0000-000000000001"), new DateTime(2026, 8, 18, 16, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 8, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), "Venta demo para visualizar movimientos.", null, "Venta", "SAL-DEMO-001", "Demo seed" });

            migrationBuilder.InsertData(
                schema: "inventory",
                table: "products",
                columns: new[] { "id", "active_ingredient", "category", "concentration", "created_at", "expiration_date", "location", "lot", "manufacturer", "maximum_stock", "minimum_stock", "name", "notes", "presentation", "product_type", "sku", "status", "stock", "supplier", "unit", "unit_cost", "updated_at" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "Acetaminofén", "Analgésicos", "500 mg", new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2027, 10, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), "Estante A-01", "PCT-DEMO-01", "Genfar", 400, 80, "Paracetamol", "Rotación alta.", "Tabletas", "Medicamento", "MED-PAR-500", "Activo", 248, "Drogas La Rebaja", "Caja x 20", 4200m, null },
                    { new Guid("10000000-0000-0000-0000-000000000002"), null, "Snacks altos en proteína", null, new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2027, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), "Estante W-01", "SNK-DEMO-01", "NutriFit", 180, 20, "Barra de proteína cacao", "Sin azúcar añadida.", "Barra", "Snack saludable", "SNK-PRO-001", "Activo", 96, "Wellness Foods", "Unidad x 50 g", 4200m, null },
                    { new Guid("10000000-0000-0000-0000-000000000003"), null, "Bebidas sin azúcar", "500 ml", new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2027, 4, 12, 0, 0, 0, 0, DateTimeKind.Unspecified), "Nevera W-01", "BEV-DEMO-01", "Vital Drinks", 140, 18, "Té frío sin azúcar", "Sabor limón.", "Botella", "Bebida", "BEV-TEA-001", "Activo", 72, "Wellness Foods", "Unidad", 3500m, null },
                    { new Guid("10000000-0000-0000-0000-000000000004"), "Psyllium", "Digestión y saciedad", "300 g", new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2028, 1, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), "Estante W-02", "SUP-DEMO-01", "BioBalance", 80, 12, "Fibra soluble", "Acompañar con suficiente agua.", "Polvo", "Suplemento", "SUP-FIB-001", "Activo", 44, "NutriSupply", "Frasco", 28500m, null },
                    { new Guid("10000000-0000-0000-0000-000000000005"), null, "Despensa saludable", null, new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2027, 11, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), "Estante W-03", "FOO-DEMO-01", "Campo Vivo", 120, 15, "Avena integral", "Ideal para desayunos.", "Hojuelas", "Alimento saludable", "FOO-OAT-001", "Activo", 65, "Healthy Market", "Bolsa x 500 g", 6800m, null },
                    { new Guid("10000000-0000-0000-0000-000000000006"), null, "Monitoreo corporal", null, new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Vitrina D-01", "DEV-DEMO-01", "HealthTrack", 30, 4, "Balanza inteligente", "Conectividad Bluetooth.", "Digital", "Dispositivo de salud", "DEV-SCL-001", "Activo", 14, "MedTech Supply", "Unidad", 118000m, null },
                    { new Guid("10000000-0000-0000-0000-000000000007"), null, "Entrenamiento en casa", null, new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Estante F-01", "FIT-DEMO-01", "MoveWell", 60, 6, "Bandas elásticas de resistencia", "Tres niveles de resistencia.", "Set", "Equipamiento fitness", "FIT-BND-001", "Activo", 28, "Active Supply", "Set x 5", 42000m, null },
                    { new Guid("10000000-0000-0000-0000-000000000008"), null, "Cuidado de la piel", "50+", new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2027, 9, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), "Estante C-01", "CARE-DEMO-01", "DermaCare", 70, 8, "Protector solar SPF 50", "Uso diario recomendado.", "Crema", "Cuidado personal", "CARE-SUN-001", "Activo", 31, "Wellness Foods", "Tubo x 120 ml", 26000m, null },
                    { new Guid("10000000-0000-0000-0000-000000000009"), null, "Monitoreo de glucosa", null, new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2027, 8, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), "Estante P-01", "PHR-DEMO-01", "GlucoSafe", 60, 8, "Tiras para glucómetro", "Compatibles con GlucoSafe.", "Tiras", "Producto de farmacia", "PHR-GLU-001", "Activo", 22, "MedTech Supply", "Caja x 50", 33000m, null },
                    { new Guid("10000000-0000-0000-0000-000000000010"), "Colecalciferol", "Vitaminas", "1000 UI", new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2028, 2, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), "Estante A-02", "MED-DEMO-01", "HealthLab", 100, 12, "Vitamina D3", "Venta libre.", "Cápsulas", "Medicamento", "MED-VIT-001", "Activo", 53, "NutriSupply", "Frasco x 60", 18500m, null },
                    { new Guid("10000000-0000-0000-0000-000000000011"), null, "Bioseguridad", "Talla M", new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2029, 6, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Bodega C-01", "INS-DEMO-01", "Medline", 180, 25, "Guantes de nitrilo", null, "Guantes", "Insumo médico", "INS-GLV-001", "Activo", 84, "Suministros Clínicos", "Caja x 100", 22000m, null },
                    { new Guid("10000000-0000-0000-0000-000000000012"), null, "Accesorios saludables", "750 ml", new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Estante F-02", "OTH-DEMO-01", "EcoMove", 100, 10, "Botella reutilizable", "Libre de BPA.", "Botella", "Otro", "OTH-BTL-001", "Activo", 40, "Active Supply", "Unidad", 28000m, null }
                });

            migrationBuilder.InsertData(
                schema: "inventory",
                table: "inventory_entry_lines",
                columns: new[] { "id", "entry_id", "expiration_date", "lot", "product_id", "product_name", "quantity", "unit_cost" },
                values: new object[,]
                {
                    { new Guid("31000000-0000-0000-0000-000000000001"), new Guid("30000000-0000-0000-0000-000000000001"), new DateTime(2027, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), "SNK-DEMO-01", new Guid("10000000-0000-0000-0000-000000000002"), "Barra de proteína cacao", 80, 4200m },
                    { new Guid("31000000-0000-0000-0000-000000000002"), new Guid("30000000-0000-0000-0000-000000000001"), new DateTime(2027, 4, 12, 0, 0, 0, 0, DateTimeKind.Unspecified), "BEV-DEMO-01", new Guid("10000000-0000-0000-0000-000000000003"), "Té frío sin azúcar", 60, 3500m },
                    { new Guid("31000000-0000-0000-0000-000000000003"), new Guid("30000000-0000-0000-0000-000000000002"), new DateTime(2028, 1, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), "SUP-DEMO-01", new Guid("10000000-0000-0000-0000-000000000004"), "Fibra soluble", 25, 28500m }
                });

            migrationBuilder.InsertData(
                schema: "inventory",
                table: "inventory_exit_lines",
                columns: new[] { "id", "exit_id", "expiration_date", "lot", "product_id", "product_name", "quantity", "unit_cost" },
                values: new object[] { new Guid("41000000-0000-0000-0000-000000000001"), new Guid("40000000-0000-0000-0000-000000000001"), new DateTime(2027, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), "SNK-DEMO-01", new Guid("10000000-0000-0000-0000-000000000002"), "Barra de proteína cacao", 8, 4200m });

            migrationBuilder.InsertData(
                schema: "inventory",
                table: "inventory_movements",
                columns: new[] { "id", "date_time", "direction", "lot", "product_id", "product_name", "quantity", "reason", "reference", "stock_after", "stock_before", "user_name" },
                values: new object[,]
                {
                    { new Guid("50000000-0000-0000-0000-000000000001"), new DateTime(2026, 8, 13, 10, 0, 0, 0, DateTimeKind.Utc), "Entrada", "SNK-DEMO-01", new Guid("10000000-0000-0000-0000-000000000002"), "Barra de proteína cacao", 80, "Compra", "ENT-DEMO-001", 96, 16, "Demo seed" },
                    { new Guid("50000000-0000-0000-0000-000000000002"), new DateTime(2026, 8, 13, 10, 0, 0, 0, DateTimeKind.Utc), "Entrada", "BEV-DEMO-01", new Guid("10000000-0000-0000-0000-000000000003"), "Té frío sin azúcar", 60, "Compra", "ENT-DEMO-001", 72, 12, "Demo seed" },
                    { new Guid("50000000-0000-0000-0000-000000000003"), new DateTime(2026, 8, 16, 14, 0, 0, 0, DateTimeKind.Utc), "Entrada", "SUP-DEMO-01", new Guid("10000000-0000-0000-0000-000000000004"), "Fibra soluble", 25, "Recepción de proveedor", "ENT-DEMO-002", 44, 19, "Demo seed" },
                    { new Guid("50000000-0000-0000-0000-000000000004"), new DateTime(2026, 8, 18, 16, 0, 0, 0, DateTimeKind.Utc), "Salida", "SNK-DEMO-01", new Guid("10000000-0000-0000-0000-000000000002"), "Barra de proteína cacao", 8, "Venta", "SAL-DEMO-001", 88, 96, "Demo seed" }
                });

            migrationBuilder.InsertData(
                schema: "store",
                table: "store_items",
                columns: new[] { "id", "created_at", "description", "featured", "product_id", "sale_price", "status", "updated_at" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Snack alto en proteína para controlar el hambre entre comidas.", true, new Guid("10000000-0000-0000-0000-000000000002"), 8900m, "Visible", null },
                    { new Guid("20000000-0000-0000-0000-000000000002"), new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Bebida refrescante sin azúcar añadida.", true, new Guid("10000000-0000-0000-0000-000000000003"), 7500m, "Visible", null }
                });

            migrationBuilder.InsertData(
                schema: "store",
                table: "store_items",
                columns: new[] { "id", "created_at", "description", "product_id", "sale_price", "status", "updated_at" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000003"), new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Apoyo de fibra para una alimentación equilibrada.", new Guid("10000000-0000-0000-0000-000000000004"), 49900m, "Visible", null },
                    { new Guid("20000000-0000-0000-0000-000000000004"), new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Base versátil para desayunos saludables.", new Guid("10000000-0000-0000-0000-000000000005"), 10900m, "Visible", null }
                });

            migrationBuilder.InsertData(
                schema: "store",
                table: "store_items",
                columns: new[] { "id", "created_at", "description", "featured", "product_id", "sale_price", "status", "updated_at" },
                values: new object[] { new Guid("20000000-0000-0000-0000-000000000005"), new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Monitorea tu progreso corporal desde casa.", true, new Guid("10000000-0000-0000-0000-000000000006"), 159000m, "Visible", null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "inventory",
                table: "inventory_entry_lines",
                keyColumn: "id",
                keyValue: new Guid("31000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                schema: "inventory",
                table: "inventory_entry_lines",
                keyColumn: "id",
                keyValue: new Guid("31000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                schema: "inventory",
                table: "inventory_entry_lines",
                keyColumn: "id",
                keyValue: new Guid("31000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                schema: "inventory",
                table: "inventory_exit_lines",
                keyColumn: "id",
                keyValue: new Guid("41000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                schema: "inventory",
                table: "inventory_movements",
                keyColumn: "id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                schema: "inventory",
                table: "inventory_movements",
                keyColumn: "id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                schema: "inventory",
                table: "inventory_movements",
                keyColumn: "id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                schema: "inventory",
                table: "inventory_movements",
                keyColumn: "id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                schema: "inventory",
                table: "products",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                schema: "inventory",
                table: "products",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                schema: "inventory",
                table: "products",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                schema: "inventory",
                table: "products",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                schema: "inventory",
                table: "products",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000010"));

            migrationBuilder.DeleteData(
                schema: "inventory",
                table: "products",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000011"));

            migrationBuilder.DeleteData(
                schema: "inventory",
                table: "products",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000012"));

            migrationBuilder.DeleteData(
                schema: "store",
                table: "store_items",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                schema: "store",
                table: "store_items",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                schema: "store",
                table: "store_items",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                schema: "store",
                table: "store_items",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                schema: "store",
                table: "store_items",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                schema: "inventory",
                table: "inventory_entries",
                keyColumn: "id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                schema: "inventory",
                table: "inventory_entries",
                keyColumn: "id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                schema: "inventory",
                table: "inventory_exits",
                keyColumn: "id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                schema: "inventory",
                table: "products",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                schema: "inventory",
                table: "products",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                schema: "inventory",
                table: "products",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                schema: "inventory",
                table: "products",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                schema: "inventory",
                table: "products",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000006"));
        }
    }
}
