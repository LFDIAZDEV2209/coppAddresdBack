using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFoodAiNutritionMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "mapping_confidence",
                schema: "foodai",
                table: "foods",
                type: "numeric(4,3)",
                precision: 4,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "mapping_status",
                schema: "foodai",
                table: "foods",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_id",
                schema: "foodai",
                table: "food_nutrition",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "mapping_confidence",
                schema: "foodai",
                table: "foods");

            migrationBuilder.DropColumn(
                name: "mapping_status",
                schema: "foodai",
                table: "foods");

            migrationBuilder.DropColumn(
                name: "source_id",
                schema: "foodai",
                table: "food_nutrition");
        }
    }
}
