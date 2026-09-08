using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientDailyMetricsAndMediaFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "chapters",
                schema: "app",
                table: "media_items",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "takeaways",
                schema: "app",
                table: "media_items",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.CreateTable(
                name: "patient_daily_metrics",
                schema: "app",
                columns: table => new
                {
                    MetricDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uuid", nullable: false),
                    MetricKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DimensionKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TotalCount = table.Column<long>(type: "bigint", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patient_daily_metrics", x => new { x.MetricDate, x.ClinicId, x.MetricKey, x.DimensionKey });
                });

            migrationBuilder.CreateIndex(
                name: "IX_patient_daily_metrics_ClinicId_MetricKey_MetricDate",
                schema: "app",
                table: "patient_daily_metrics",
                columns: new[] { "ClinicId", "MetricKey", "MetricDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "patient_daily_metrics",
                schema: "app");

            migrationBuilder.DropColumn(
                name: "chapters",
                schema: "app",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "takeaways",
                schema: "app",
                table: "media_items");
        }
    }
}
