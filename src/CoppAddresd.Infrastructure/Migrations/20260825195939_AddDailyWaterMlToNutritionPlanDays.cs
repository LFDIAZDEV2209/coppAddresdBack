using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyWaterMlToNutritionPlanDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "daily_water_ml",
                schema: "app",
                table: "nutrition_plan_days",
                type: "integer",
                nullable: false,
                defaultValue: 2000);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "daily_water_ml",
                schema: "app",
                table: "nutrition_plan_days");
        }
    }
}
