// Migracion aditiva del schema app; no modifica ni elimina tablas legacy.
// Rollback: preferir volver al binario anterior dejando estas tablas intactas.
// Down elimina solo estas cuatro tablas nuevas: usar unicamente antes de prescribir
// o tras respaldo y autorizacion explicita del owner si ya contienen datos clinicos.
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNutritionPrescriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "nutrition_food_items",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    icon = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    food_group = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    calories_100 = table.Column<double>(type: "double precision", nullable: false),
                    carbs_100 = table.Column<double>(type: "double precision", nullable: false),
                    protein_100 = table.Column<double>(type: "double precision", nullable: false),
                    fat_100 = table.Column<double>(type: "double precision", nullable: false),
                    fiber_100 = table.Column<double>(type: "double precision", nullable: false),
                    unit_singular = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    unit_plural = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    grams_per_unit = table.Column<double>(type: "double precision", nullable: true),
                    unit_step = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nutrition_food_items", x => x.id);
                    table.CheckConstraint("ck_nutrition_food_group", "food_group IN ('C','P','F','V')");
                    table.CheckConstraint("ck_nutrition_food_nutrients", "calories_100 >= 0 AND carbs_100 >= 0 AND protein_100 >= 0 AND fat_100 >= 0 AND fiber_100 >= 0 AND calories_100 < 'Infinity'::float8 AND carbs_100 < 'Infinity'::float8 AND protein_100 < 'Infinity'::float8 AND fat_100 < 'Infinity'::float8 AND fiber_100 < 'Infinity'::float8");
                    table.CheckConstraint("ck_nutrition_food_unit", "(unit_singular IS NULL AND unit_plural IS NULL AND grams_per_unit IS NULL AND unit_step IS NULL) OR (unit_singular IS NOT NULL AND unit_plural IS NOT NULL AND grams_per_unit IS NOT NULL AND unit_step IS NOT NULL AND grams_per_unit > 0 AND grams_per_unit < 'Infinity'::float8 AND unit_step > 0 AND unit_step < 'Infinity'::float8)");
                });

            migrationBuilder.CreateTable(
                name: "nutrition_prescriptions",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cycle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    provisional = table.Column<bool>(type: "boolean", nullable: false),
                    is_published = table.Column<bool>(type: "boolean", nullable: false),
                    menus_outdated = table.Column<bool>(type: "boolean", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    calorie_target = table.Column<double>(type: "double precision", nullable: true),
                    carbs_target = table.Column<double>(type: "double precision", nullable: true),
                    protein_target = table.Column<double>(type: "double precision", nullable: true),
                    fat_target = table.Column<double>(type: "double precision", nullable: true),
                    fiber_target = table.Column<double>(type: "double precision", nullable: true),
                    water_ml = table.Column<int>(type: "integer", nullable: true),
                    instructions = table.Column<string>(type: "text", nullable: true),
                    instructions_en = table.Column<string>(type: "text", nullable: true),
                    return_comment = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nutrition_prescriptions", x => x.id);
                    table.CheckConstraint("ck_nutrition_prescription_publication", "NOT is_published OR state = 'aprobado'");
                    table.CheckConstraint("ck_nutrition_prescription_approval", "state <> 'aprobado' OR (start_date IS NOT NULL AND end_date IS NOT NULL AND approved_by IS NOT NULL AND approved_at IS NOT NULL)");
                    table.CheckConstraint("ck_nutrition_prescription_dates", "(start_date IS NULL AND end_date IS NULL) OR (start_date IS NOT NULL AND end_date IS NOT NULL AND end_date >= start_date AND end_date - start_date < 30)");
                    table.CheckConstraint("ck_nutrition_prescription_state", "state IN ('borrador_ia','en_aprobacion','aprobado','devuelto')");
                    table.CheckConstraint("ck_nutrition_prescription_version", "version > 0 AND revision > 0");
                    table.ForeignKey(
                        name: "FK_nutrition_prescriptions_patient_profiles_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "nutrition_prescription_meals",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    prescription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_number = table.Column<int>(type: "integer", nullable: false),
                    meal_type = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    scheduled_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    menu_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    water_ml = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nutrition_prescription_meals", x => x.id);
                    table.CheckConstraint("ck_nutrition_meal_day", "day_number BETWEEN 1 AND 30");
                    table.CheckConstraint("ck_nutrition_meal_type", "(meal_type = 'des' AND scheduled_time = TIME '07:00') OR (meal_type = 'alm' AND scheduled_time = TIME '12:00') OR (meal_type = 'mer' AND scheduled_time = TIME '15:30') OR (meal_type = 'cen' AND scheduled_time = TIME '19:00')");
                    table.ForeignKey(
                        name: "FK_nutrition_prescription_meals_nutrition_prescriptions_prescr~",
                        column: x => x.prescription_id,
                        principalSchema: "app",
                        principalTable: "nutrition_prescriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "nutrition_prescription_items",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    meal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    food_item_id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    grams = table.Column<double>(type: "double precision", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nutrition_prescription_items", x => x.id);
                    table.CheckConstraint("ck_nutrition_item_grams", "grams > 0 AND grams < 'Infinity'::float8");
                    table.ForeignKey(
                        name: "FK_nutrition_prescription_items_nutrition_food_items_food_item~",
                        column: x => x.food_item_id,
                        principalSchema: "app",
                        principalTable: "nutrition_food_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_nutrition_prescription_items_nutrition_prescription_meals_m~",
                        column: x => x.meal_id,
                        principalSchema: "app",
                        principalTable: "nutrition_prescription_meals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_nutrition_items_food",
                schema: "app",
                table: "nutrition_prescription_items",
                column: "food_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_nutrition_items_meal_order",
                schema: "app",
                table: "nutrition_prescription_items",
                columns: new[] { "meal_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_nutrition_meals_prescription_day_type",
                schema: "app",
                table: "nutrition_prescription_meals",
                columns: new[] { "prescription_id", "day_number", "meal_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_nutrition_prescriptions_patient_state_validity",
                schema: "app",
                table: "nutrition_prescriptions",
                columns: new[] { "patient_id", "state", "start_date", "end_date" });

            migrationBuilder.CreateIndex(
                name: "ix_nutrition_prescriptions_state_end",
                schema: "app",
                table: "nutrition_prescriptions",
                columns: new[] { "state", "end_date" });

            migrationBuilder.CreateIndex(
                name: "uq_nutrition_prescriptions_cycle_version",
                schema: "app",
                table: "nutrition_prescriptions",
                columns: new[] { "cycle_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_nutrition_prescriptions_published_cycle",
                schema: "app",
                table: "nutrition_prescriptions",
                column: "cycle_id",
                unique: true,
                filter: "is_published = TRUE");
            using var seed = typeof(AddNutritionPrescriptions).Assembly.GetManifestResourceStream(
                "CoppAddresd.Infrastructure.Migrations.Seed.AddNutritionFoodCatalog.sql")
                ?? throw new InvalidOperationException("No se encuentra el seed nutricional embebido.");
            using var reader = new System.IO.StreamReader(seed);
            migrationBuilder.Sql(reader.ReadToEnd());

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "nutrition_prescription_items",
                schema: "app");

            migrationBuilder.DropTable(
                name: "nutrition_food_items",
                schema: "app");

            migrationBuilder.DropTable(
                name: "nutrition_prescription_meals",
                schema: "app");

            migrationBuilder.DropTable(
                name: "nutrition_prescriptions",
                schema: "app");
        }
    }
}
