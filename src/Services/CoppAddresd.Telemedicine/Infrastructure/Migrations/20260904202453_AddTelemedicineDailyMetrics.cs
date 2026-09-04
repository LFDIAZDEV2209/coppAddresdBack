using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Telemedicine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTelemedicineDailyMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "appointment_daily_metrics",
                schema: "tele",
                columns: table => new
                {
                    metric_date = table.Column<DateOnly>(type: "date", nullable: false),
                    professional_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("00000000-0000-0000-0000-000000000000")),
                    metric_key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    dimension_key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, defaultValue: "general"),
                    clinic_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_count = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    last_updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_appointment_daily_metrics", x => new { x.metric_date, x.professional_id, x.metric_key, x.dimension_key });
                });

            migrationBuilder.CreateTable(
                name: "professional_daily_stats",
                schema: "tele",
                columns: table => new
                {
                    professional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    metric_date = table.Column<DateOnly>(type: "date", nullable: false),
                    clinic_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_appointments = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    completed_appointments = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cancelled_appointments = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    no_show_appointments = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    unique_patients = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_professional_daily_stats", x => new { x.professional_id, x.metric_date });
                });

            migrationBuilder.CreateIndex(
                name: "ix_appointment_daily_metrics_lookup",
                schema: "tele",
                table: "appointment_daily_metrics",
                columns: new[] { "metric_date", "professional_id", "metric_key" });

            migrationBuilder.CreateIndex(
                name: "ix_professional_daily_stats_date",
                schema: "tele",
                table: "professional_daily_stats",
                column: "metric_date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "appointment_daily_metrics",
                schema: "tele");

            migrationBuilder.DropTable(
                name: "professional_daily_stats",
                schema: "tele");
        }
    }
}
