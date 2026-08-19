using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryAndStoreModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.EnsureSchema(
                name: "store");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                name: "inventory_entries",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "inventory_exits",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "products",
                schema: "inventory");
        }
    }
}
