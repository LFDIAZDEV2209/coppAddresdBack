using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFoodAiNutrition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "foodai");

            migrationBuilder.CreateTable(
                name: "foods",
                schema: "foodai",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_foods", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "food_aliases",
                schema: "foodai",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    FoodId = table.Column<Guid>(type: "uuid", nullable: false),
                    alias = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_food_aliases", x => x.id);
                    table.ForeignKey(
                        name: "FK_food_aliases_foods_FoodId",
                        column: x => x.FoodId,
                        principalSchema: "foodai",
                        principalTable: "foods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "food_nutrition",
                schema: "foodai",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    FoodId = table.Column<Guid>(type: "uuid", nullable: false),
                    serving_grams = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    calories = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    protein = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    carbohydrates = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    fat = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    fiber = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    sugar = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    sodium = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    source_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    imported_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_food_nutrition", x => x.id);
                    table.ForeignKey(
                        name: "FK_food_nutrition_foods_FoodId",
                        column: x => x.FoodId,
                        principalSchema: "foodai",
                        principalTable: "foods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_food_aliases_alias",
                schema: "foodai",
                table: "food_aliases",
                column: "alias",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_food_aliases_FoodId",
                schema: "foodai",
                table: "food_aliases",
                column: "FoodId");

            migrationBuilder.CreateIndex(
                name: "ix_food_nutrition_food_id_imported_at",
                schema: "foodai",
                table: "food_nutrition",
                columns: new[] { "FoodId", "imported_at" });

            migrationBuilder.CreateIndex(
                name: "ix_foods_name",
                schema: "foodai",
                table: "foods",
                column: "name",
                unique: true);

            migrationBuilder.Sql("""
                GRANT USAGE ON SCHEMA foodai TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON foodai.foods TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON foodai.food_nutrition TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON foodai.food_aliases TO app_user;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "food_aliases",
                schema: "foodai");

            migrationBuilder.DropTable(
                name: "food_nutrition",
                schema: "foodai");

            migrationBuilder.DropTable(
                name: "foods",
                schema: "foodai");
        }
    }
}
