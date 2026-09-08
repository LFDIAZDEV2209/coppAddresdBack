using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryDailyMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_daily_metrics",
                schema: "erp",
                columns: table => new
                {
                    metric_date = table.Column<DateOnly>(type: "date", nullable: false),
                    metric_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    dimension_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    total_count = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    last_updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_daily_metrics", x => new { x.metric_date, x.metric_key, x.dimension_key });
                });

            migrationBuilder.CreateIndex(
                name: "ix_inventory_daily_metrics_date",
                schema: "erp",
                table: "inventory_daily_metrics",
                column: "metric_date");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_daily_metrics_key_dim_date",
                schema: "erp",
                table: "inventory_daily_metrics",
                columns: new[] { "metric_key", "dimension_key", "metric_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_daily_metrics",
                schema: "erp");
        }
    }
}
