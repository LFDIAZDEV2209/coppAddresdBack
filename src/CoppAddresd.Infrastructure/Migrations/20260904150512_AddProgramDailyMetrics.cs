using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramDailyMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "program_daily_metrics",
                schema: "app",
                columns: table => new
                {
                    metric_date = table.Column<DateOnly>(type: "date", nullable: false),
                    metric_key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    dimension_key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, defaultValue: "general"),
                    clinic_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_count = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    total_value = table.Column<decimal>(type: "numeric(14,2)", nullable: false, defaultValue: 0.00m),
                    last_updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_daily_metrics", x => new { x.metric_date, x.metric_key, x.dimension_key });
                });

            migrationBuilder.CreateIndex(
                name: "ix_program_daily_metrics_lookup",
                schema: "app",
                table: "program_daily_metrics",
                columns: new[] { "metric_date", "metric_key" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "program_daily_metrics",
                schema: "app");
        }
    }
}
