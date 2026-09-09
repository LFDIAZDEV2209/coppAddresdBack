using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBiometriaDailyMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "biometria_daily_metrics",
                schema: "app",
                columns: table => new
                {
                    metric_date = table.Column<DateOnly>(type: "date", nullable: false),
                    metric_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    dimension_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    total_count = table.Column<long>(type: "bigint", nullable: false),
                    total_value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    last_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_biometria_daily_metrics", x => new { x.metric_date, x.metric_key, x.dimension_key });
                });

            migrationBuilder.CreateIndex(
                name: "ix_biometria_daily_metrics_metric_key_metric_date",
                schema: "app",
                table: "biometria_daily_metrics",
                columns: new[] { "metric_key", "metric_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "biometria_daily_metrics",
                schema: "app");
        }
    }
}
