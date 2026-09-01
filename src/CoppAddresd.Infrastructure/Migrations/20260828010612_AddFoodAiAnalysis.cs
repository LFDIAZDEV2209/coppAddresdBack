using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFoodAiAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "food_analyses",
                schema: "foodai",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    analysis_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    image_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    detector_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    segmenter_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    classifier_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    portion_method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    depth_model_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    summary_calories = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    summary_protein = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    summary_carbohydrates = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    summary_fat = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    summary_fiber = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    summary_sugar = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    summary_sodium = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    source_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_food_analyses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "food_analysis_feedback",
                schema: "foodai",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    analysis_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    feedback_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    original_food = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    corrected_food = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    original_grams = table.Column<int>(type: "integer", nullable: true),
                    corrected_grams = table.Column<int>(type: "integer", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_food_analysis_feedback", x => x.id);
                    table.ForeignKey(
                        name: "FK_food_analysis_feedback_food_analyses_analysis_id",
                        column: x => x.analysis_id,
                        principalSchema: "foodai",
                        principalTable: "food_analyses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "food_analysis_items",
                schema: "foodai",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    analysis_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_index = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    detection_confidence = table.Column<double>(type: "double precision", precision: 6, scale: 4, nullable: false),
                    bbox_x = table.Column<int>(type: "integer", nullable: false),
                    bbox_y = table.Column<int>(type: "integer", nullable: false),
                    bbox_width = table.Column<int>(type: "integer", nullable: false),
                    bbox_height = table.Column<int>(type: "integer", nullable: false),
                    mask_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    mask_area_pixels = table.Column<int>(type: "integer", nullable: true),
                    portion_size = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    estimated_grams = table.Column<int>(type: "integer", nullable: true),
                    min_grams = table.Column<int>(type: "integer", nullable: true),
                    max_grams = table.Column<int>(type: "integer", nullable: true),
                    portion_confidence = table.Column<double>(type: "double precision", precision: 6, scale: 4, nullable: true),
                    portion_method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    nutrition_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    calories = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    protein = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    carbohydrates = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    fat = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    fiber = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    sugar = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    sodium = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    source_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_food_analysis_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_food_analysis_items_food_analyses_analysis_id",
                        column: x => x.analysis_id,
                        principalSchema: "foodai",
                        principalTable: "food_analyses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_food_analyses_analysis_id",
                schema: "foodai",
                table: "food_analyses",
                column: "analysis_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_food_analyses_user_id",
                schema: "foodai",
                table: "food_analyses",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_food_analysis_feedback_analysis_id_created_at",
                schema: "foodai",
                table: "food_analysis_feedback",
                columns: new[] { "analysis_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_food_analysis_items_analysis_id_item_index",
                schema: "foodai",
                table: "food_analysis_items",
                columns: new[] { "analysis_id", "item_index" },
                unique: true);

            migrationBuilder.Sql("""
                GRANT USAGE ON SCHEMA foodai TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON foodai.food_analyses TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON foodai.food_analysis_items TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON foodai.food_analysis_feedback TO app_user;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "food_analysis_feedback",
                schema: "foodai");

            migrationBuilder.DropTable(
                name: "food_analysis_items",
                schema: "foodai");

            migrationBuilder.DropTable(
                name: "food_analyses",
                schema: "foodai");
        }
    }
}
