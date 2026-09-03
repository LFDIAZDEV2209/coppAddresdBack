using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNutritionIntakeLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_food_analyses_analysis_id",
                schema: "foodai",
                table: "food_analyses",
                column: "analysis_id");

            migrationBuilder.CreateTable(
                name: "nutrition_intake_logs",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    habit_check_id = table.Column<Guid>(type: "uuid", nullable: false),
                    local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    meal_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    calories = table.Column<int>(type: "integer", nullable: true),
                    protein_g = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    carbs_g = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    fat_g = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    fiber_g = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    water_ml = table.Column<int>(type: "integer", nullable: true),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "manual"),
                    food_analysis_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nutrition_plan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nutrition_plan_day_number = table.Column<short>(type: "smallint", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nutrition_intake_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_nutrition_intake_logs_food_analyses_food_analysis_id",
                        column: x => x.food_analysis_id,
                        principalSchema: "foodai",
                        principalTable: "food_analyses",
                        principalColumn: "analysis_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_nutrition_intake_logs_habit_checks_habit_check_id",
                        column: x => x.habit_check_id,
                        principalSchema: "app",
                        principalTable: "habit_checks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_nutrition_intake_logs_nutrition_plans_nutrition_plan_id",
                        column: x => x.nutrition_plan_id,
                        principalSchema: "app",
                        principalTable: "nutrition_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_nutrition_intake_logs_patient_profiles_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_intake_logs_patient_date",
                schema: "app",
                table: "nutrition_intake_logs",
                columns: new[] { "patient_id", "local_date" });

            migrationBuilder.CreateIndex(
                name: "IX_nutrition_intake_logs_food_analysis_id",
                schema: "app",
                table: "nutrition_intake_logs",
                column: "food_analysis_id");

            migrationBuilder.CreateIndex(
                name: "IX_nutrition_intake_logs_habit_check_id",
                schema: "app",
                table: "nutrition_intake_logs",
                column: "habit_check_id");

            migrationBuilder.CreateIndex(
                name: "IX_nutrition_intake_logs_nutrition_plan_id",
                schema: "app",
                table: "nutrition_intake_logs",
                column: "nutrition_plan_id");

            migrationBuilder.CreateIndex(
                name: "uq_intake_logs_patient_date_meal",
                schema: "app",
                table: "nutrition_intake_logs",
                columns: new[] { "patient_id", "local_date", "meal_code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "nutrition_intake_logs",
                schema: "app");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_food_analyses_analysis_id",
                schema: "foodai",
                table: "food_analyses");
        }
    }
}
