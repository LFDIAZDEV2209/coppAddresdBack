using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveInventoryAndStoreToErp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "store_items",
                schema: "store",
                newName: "store_items",
                newSchema: "erp");

            migrationBuilder.RenameTable(
                name: "products",
                schema: "inventory",
                newName: "products",
                newSchema: "erp");

            migrationBuilder.RenameTable(
                name: "inventory_movements",
                schema: "inventory",
                newName: "inventory_movements",
                newSchema: "erp");

            migrationBuilder.RenameTable(
                name: "inventory_exits",
                schema: "inventory",
                newName: "inventory_exits",
                newSchema: "erp");

            migrationBuilder.RenameTable(
                name: "inventory_exit_lines",
                schema: "inventory",
                newName: "inventory_exit_lines",
                newSchema: "erp");

            migrationBuilder.RenameTable(
                name: "inventory_entry_lines",
                schema: "inventory",
                newName: "inventory_entry_lines",
                newSchema: "erp");

            migrationBuilder.RenameTable(
                name: "inventory_entries",
                schema: "inventory",
                newName: "inventory_entries",
                newSchema: "erp");

            migrationBuilder.Sql("DROP SCHEMA IF EXISTS inventory; DROP SCHEMA IF EXISTS store;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.EnsureSchema(
                name: "store");

            migrationBuilder.RenameTable(
                name: "store_items",
                schema: "erp",
                newName: "store_items",
                newSchema: "store");

            migrationBuilder.RenameTable(
                name: "products",
                schema: "erp",
                newName: "products",
                newSchema: "inventory");

            migrationBuilder.RenameTable(
                name: "inventory_movements",
                schema: "erp",
                newName: "inventory_movements",
                newSchema: "inventory");

            migrationBuilder.RenameTable(
                name: "inventory_exits",
                schema: "erp",
                newName: "inventory_exits",
                newSchema: "inventory");

            migrationBuilder.RenameTable(
                name: "inventory_exit_lines",
                schema: "erp",
                newName: "inventory_exit_lines",
                newSchema: "inventory");

            migrationBuilder.RenameTable(
                name: "inventory_entry_lines",
                schema: "erp",
                newName: "inventory_entry_lines",
                newSchema: "inventory");

            migrationBuilder.RenameTable(
                name: "inventory_entries",
                schema: "erp",
                newName: "inventory_entries",
                newSchema: "inventory");
        }
    }
}
