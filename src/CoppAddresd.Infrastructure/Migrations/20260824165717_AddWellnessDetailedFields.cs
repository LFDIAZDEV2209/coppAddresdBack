using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWellnessDetailedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "equipment",
                schema: "app",
                table: "routine_exercises",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "rpe",
                schema: "app",
                table: "routine_exercises",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "target_muscle",
                schema: "app",
                table: "routine_exercises",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tempo",
                schema: "app",
                table: "routine_exercises",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tips",
                schema: "app",
                table: "routine_exercises",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "allergens",
                schema: "app",
                table: "nutrition_plans",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "daily_carbs_target",
                schema: "app",
                table: "nutrition_plans",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "daily_fat_target",
                schema: "app",
                table: "nutrition_plans",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "daily_fiber_target",
                schema: "app",
                table: "nutrition_plans",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "daily_protein_target",
                schema: "app",
                table: "nutrition_plans",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "meal_timing",
                schema: "app",
                table: "nutrition_plans",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "carbs_g",
                schema: "app",
                table: "nutrition_plan_days",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "fat_g",
                schema: "app",
                table: "nutrition_plan_days",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "fiber_g",
                schema: "app",
                table: "nutrition_plan_days",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "protein_g",
                schema: "app",
                table: "nutrition_plan_days",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "water_ml",
                schema: "app",
                table: "nutrition_plan_days",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cooldown_notes",
                schema: "app",
                table: "exercise_routines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "equipment",
                schema: "app",
                table: "exercise_routines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "target_muscles",
                schema: "app",
                table: "exercise_routines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "warmup_notes",
                schema: "app",
                table: "exercise_routines",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "equipment",
                schema: "app",
                table: "routine_exercises");

            migrationBuilder.DropColumn(
                name: "rpe",
                schema: "app",
                table: "routine_exercises");

            migrationBuilder.DropColumn(
                name: "target_muscle",
                schema: "app",
                table: "routine_exercises");

            migrationBuilder.DropColumn(
                name: "tempo",
                schema: "app",
                table: "routine_exercises");

            migrationBuilder.DropColumn(
                name: "tips",
                schema: "app",
                table: "routine_exercises");

            migrationBuilder.DropColumn(
                name: "allergens",
                schema: "app",
                table: "nutrition_plans");

            migrationBuilder.DropColumn(
                name: "daily_carbs_target",
                schema: "app",
                table: "nutrition_plans");

            migrationBuilder.DropColumn(
                name: "daily_fat_target",
                schema: "app",
                table: "nutrition_plans");

            migrationBuilder.DropColumn(
                name: "daily_fiber_target",
                schema: "app",
                table: "nutrition_plans");

            migrationBuilder.DropColumn(
                name: "daily_protein_target",
                schema: "app",
                table: "nutrition_plans");

            migrationBuilder.DropColumn(
                name: "meal_timing",
                schema: "app",
                table: "nutrition_plans");

            migrationBuilder.DropColumn(
                name: "carbs_g",
                schema: "app",
                table: "nutrition_plan_days");

            migrationBuilder.DropColumn(
                name: "fat_g",
                schema: "app",
                table: "nutrition_plan_days");

            migrationBuilder.DropColumn(
                name: "fiber_g",
                schema: "app",
                table: "nutrition_plan_days");

            migrationBuilder.DropColumn(
                name: "protein_g",
                schema: "app",
                table: "nutrition_plan_days");

            migrationBuilder.DropColumn(
                name: "water_ml",
                schema: "app",
                table: "nutrition_plan_days");

            migrationBuilder.DropColumn(
                name: "cooldown_notes",
                schema: "app",
                table: "exercise_routines");

            migrationBuilder.DropColumn(
                name: "equipment",
                schema: "app",
                table: "exercise_routines");

            migrationBuilder.DropColumn(
                name: "target_muscles",
                schema: "app",
                table: "exercise_routines");

            migrationBuilder.DropColumn(
                name: "warmup_notes",
                schema: "app",
                table: "exercise_routines");
        }
    }
}
