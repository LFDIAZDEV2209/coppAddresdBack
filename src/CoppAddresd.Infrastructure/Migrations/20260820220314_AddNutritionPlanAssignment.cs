using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNutritionPlanAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "nutrition_plan_assignments",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date = table.Column<DateTime>(type: "date", nullable: false),
                    end_date = table.Column<DateTime>(type: "date", nullable: true),
                    status = table.Column<int>(type: "integer", maxLength: 20, nullable: false, defaultValue: 1),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nutrition_plan_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_nutrition_plan_assignments_nutrition_plans_plan_id",
                        column: x => x.plan_id,
                        principalSchema: "app",
                        principalTable: "nutrition_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_nutrition_plan_assignments_patient_profiles_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_nutrition_plan_assignments_patient_id",
                schema: "app",
                table: "nutrition_plan_assignments",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_nutrition_plan_assignments_patient_status",
                schema: "app",
                table: "nutrition_plan_assignments",
                columns: new[] { "patient_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_nutrition_plan_assignments_plan_id",
                schema: "app",
                table: "nutrition_plan_assignments",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_nutrition_plan_assignments_status",
                schema: "app",
                table: "nutrition_plan_assignments",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "nutrition_plan_assignments",
                schema: "app");
        }
    }
}
