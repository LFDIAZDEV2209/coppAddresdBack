using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanSafetyRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "plan_safety_rules",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    metric_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    @operator = table.Column<string>(name: "operator", type: "character varying(10)", maxLength: 10, nullable: false),
                    threshold_min = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    threshold_max = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    unit_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    restriction = table.Column<string>(type: "text", nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_safety_rules", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_plan_safety_rules_active_metric",
                schema: "app",
                table: "plan_safety_rules",
                columns: new[] { "is_active", "metric_code" });

            migrationBuilder.CreateIndex(
                name: "ix_plan_safety_rules_metric_code",
                schema: "app",
                table: "plan_safety_rules",
                column: "metric_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "plan_safety_rules",
                schema: "app");
        }
    }
}
