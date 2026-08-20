using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.EnsureSchema(
                name: "store");

            migrationBuilder.CreateTable(
                name: "document_categories",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inventory_entries",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    reference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    date = table.Column<DateTime>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    supplier = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    document = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    responsible = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    total_cost = table.Column<decimal>(type: "numeric(14,2)", nullable: false, defaultValue: 0m),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inventory_exits",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    reference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    date = table.Column<DateTime>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    responsible = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    patient_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_exits", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    sku = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    product_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    active_ingredient = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    presentation = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    concentration = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    unit = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    manufacturer = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    supplier = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    lot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    expiration_date = table.Column<DateTime>(type: "date", nullable: true),
                    stock = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    minimum_stock = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    maximum_stock = table.Column<int>(type: "integer", nullable: false, defaultValue: 100),
                    location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Activo"),
                    unit_cost = table.Column<decimal>(type: "numeric(12,2)", nullable: false, defaultValue: 0m),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "clinical_document_types",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    allowed_extensions = table.Column<List<string>>(type: "text[]", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clinical_document_types", x => x.id);
                    table.ForeignKey(
                        name: "FK_clinical_document_types_document_categories_category_id",
                        column: x => x.category_id,
                        principalSchema: "app",
                        principalTable: "document_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_entry_lines",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    lot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    expiration_date = table.Column<DateTime>(type: "date", nullable: true),
                    unit_cost = table.Column<decimal>(type: "numeric(12,2)", nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_entry_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_inventory_entry_lines_inventory_entries_entry_id",
                        column: x => x.entry_id,
                        principalSchema: "inventory",
                        principalTable: "inventory_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventory_entry_lines_products_product_id",
                        column: x => x.product_id,
                        principalSchema: "inventory",
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_exit_lines",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    exit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    lot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    expiration_date = table.Column<DateTime>(type: "date", nullable: true),
                    unit_cost = table.Column<decimal>(type: "numeric(12,2)", nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_exit_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_inventory_exit_lines_inventory_exits_exit_id",
                        column: x => x.exit_id,
                        principalSchema: "inventory",
                        principalTable: "inventory_exits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventory_exit_lines_products_product_id",
                        column: x => x.product_id,
                        principalSchema: "inventory",
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_movements",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    date_time = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    stock_before = table.Column<int>(type: "integer", nullable: false),
                    stock_after = table.Column<int>(type: "integer", nullable: false),
                    lot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    user_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    reason = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    reference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_movements", x => x.id);
                    table.ForeignKey(
                        name: "FK_inventory_movements_products_product_id",
                        column: x => x.product_id,
                        principalSchema: "inventory",
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "store_items",
                schema: "store",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sale_price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    featured = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Visible"),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_store_items_products_product_id",
                        column: x => x.product_id,
                        principalSchema: "inventory",
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: true),
                    professional_id = table.Column<Guid>(type: "uuid", nullable: true),
                    clinic_id = table.Column<Guid>(type: "uuid", nullable: true),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    encounter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    document_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    parent_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Ready"),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents1", x => x.id);
                    table.ForeignKey(
                        name: "FK_documents_clinical_document_types_document_type_id",
                        column: x => x.document_type_id,
                        principalSchema: "app",
                        principalTable: "clinical_document_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documents_clinics_clinic_id",
                        column: x => x.clinic_id,
                        principalSchema: "erp",
                        principalTable: "clinics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_documents_documents_parent_document_id",
                        column: x => x.parent_document_id,
                        principalSchema: "app",
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documents_locations_location_id",
                        column: x => x.location_id,
                        principalSchema: "erp",
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_documents_patient_profiles_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documents_professionals_professional_id",
                        column: x => x.professional_id,
                        principalSchema: "erp",
                        principalTable: "professionals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

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

            migrationBuilder.CreateIndex(
                name: "ix_clinical_document_types_category_id",
                schema: "app",
                table: "clinical_document_types",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_clinical_document_types_code",
                schema: "app",
                table: "clinical_document_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_categories_code",
                schema: "app",
                table: "document_categories",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_documents_clinic_id",
                schema: "app",
                table: "documents",
                column: "clinic_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_deleted_at",
                schema: "app",
                table: "documents",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "ix_documents_document_type_id",
                schema: "app",
                table: "documents",
                column: "document_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_documents_location_id",
                schema: "app",
                table: "documents",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_parent_document_id",
                schema: "app",
                table: "documents",
                column: "parent_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_patient_id",
                schema: "app",
                table: "documents",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "IX_documents_professional_id",
                schema: "app",
                table: "documents",
                column: "professional_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_status",
                schema: "app",
                table: "documents",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_entries_date",
                schema: "inventory",
                table: "inventory_entries",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_entries_reference",
                schema: "inventory",
                table: "inventory_entries",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_entry_lines_entry_id",
                schema: "inventory",
                table: "inventory_entry_lines",
                column: "entry_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_entry_lines_product_id",
                schema: "inventory",
                table: "inventory_entry_lines",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_exit_lines_exit_id",
                schema: "inventory",
                table: "inventory_exit_lines",
                column: "exit_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_exit_lines_product_id",
                schema: "inventory",
                table: "inventory_exit_lines",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_exits_date",
                schema: "inventory",
                table: "inventory_exits",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_exits_reference",
                schema: "inventory",
                table: "inventory_exits",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inventory_movements_date_time",
                schema: "inventory",
                table: "inventory_movements",
                column: "date_time");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_movements_direction",
                schema: "inventory",
                table: "inventory_movements",
                column: "direction");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_movements_product_id",
                schema: "inventory",
                table: "inventory_movements",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_products_category",
                schema: "inventory",
                table: "products",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "ix_products_product_type",
                schema: "inventory",
                table: "products",
                column: "product_type");

            migrationBuilder.CreateIndex(
                name: "ix_products_sku",
                schema: "inventory",
                table: "products",
                column: "sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_products_status",
                schema: "inventory",
                table: "products",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_store_items_featured",
                schema: "store",
                table: "store_items",
                column: "featured");

            migrationBuilder.CreateIndex(
                name: "ix_store_items_product_id",
                schema: "store",
                table: "store_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_store_items_status",
                schema: "store",
                table: "store_items",
                column: "status");

            // FKs hacia auth.users (creadas por SQL, patrón user_id): la tabla
            // Users vive en el schema auth y la gestiona el Auth Service.
            migrationBuilder.Sql("""
                ALTER TABLE app.documents
                    ADD CONSTRAINT fk_documents_uploaded_by
                    FOREIGN KEY (uploaded_by) REFERENCES auth."Users" ("Id")
                    ON DELETE SET NULL;

                ALTER TABLE app.documents
                    ADD CONSTRAINT fk_documents_created_by
                    FOREIGN KEY (created_by) REFERENCES auth."Users" ("Id")
                    ON DELETE SET NULL;

                ALTER TABLE app.documents
                    ADD CONSTRAINT fk_documents_updated_by
                    FOREIGN KEY (updated_by) REFERENCES auth."Users" ("Id")
                    ON DELETE SET NULL;

                ALTER TABLE app.documents
                    ADD CONSTRAINT fk_documents_deleted_by
                    FOREIGN KEY (deleted_by) REFERENCES auth."Users" ("Id")
                    ON DELETE SET NULL;
                """);

            // Catálogos de documentos (categorías + tipos con extensiones).
            migrationBuilder.Sql(ReadSeedScript());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE app.documents DROP CONSTRAINT IF EXISTS fk_documents_uploaded_by;
                ALTER TABLE app.documents DROP CONSTRAINT IF EXISTS fk_documents_created_by;
                ALTER TABLE app.documents DROP CONSTRAINT IF EXISTS fk_documents_updated_by;
                ALTER TABLE app.documents DROP CONSTRAINT IF EXISTS fk_documents_deleted_by;
                """);

            migrationBuilder.DropTable(
                name: "documents",
                schema: "app");

            migrationBuilder.DropTable(
                name: "inventory_entry_lines",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "inventory_exit_lines",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "inventory_movements",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "store_items",
                schema: "store");

            migrationBuilder.DropTable(
                name: "clinical_document_types",
                schema: "app");

            migrationBuilder.DropTable(
                name: "inventory_entries",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "inventory_exits",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "products",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "document_categories",
                schema: "app");
        }

        /// <summary>Lee el seed de catálogos de documentos embebido (recurso EmbeddedResource).</summary>
        private static string ReadSeedScript()
        {
            var assembly = typeof(AddPatientDocuments).Assembly;
            using var stream = assembly.GetManifestResourceStream(
                    "CoppAddresd.Infrastructure.Migrations.Seed.AddDocumentCatalogs.sql")
                ?? throw new InvalidOperationException(
                    "Recurso embebido 'AddDocumentCatalogs.sql' no encontrado.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
