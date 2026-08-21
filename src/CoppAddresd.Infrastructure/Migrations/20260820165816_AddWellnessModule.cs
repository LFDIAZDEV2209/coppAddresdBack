using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWellnessModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "exercise_routines",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    difficulty = table.Column<int>(type: "integer", maxLength: 20, nullable: false),
                    estimated_minutes = table.Column<int>(type: "integer", nullable: true),
                    category = table.Column<int>(type: "integer", maxLength: 20, nullable: false),
                    status = table.Column<int>(type: "integer", maxLength: 20, nullable: false, defaultValue: 1),
                    media_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercise_routines", x => x.id);
                    table.ForeignKey(
                        name: "FK_exercise_routines_media_items_media_id",
                        column: x => x.media_id,
                        principalSchema: "app",
                        principalTable: "media_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "nutrition_plans",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    target_condition = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    duration_days = table.Column<int>(type: "integer", nullable: false),
                    daily_calorie_target = table.Column<int>(type: "integer", nullable: true),
                    is_template = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_plan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", maxLength: 20, nullable: false, defaultValue: 1),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nutrition_plans", x => x.id);
                    table.ForeignKey(
                        name: "FK_nutrition_plans_nutrition_plans_source_plan_id",
                        column: x => x.source_plan_id,
                        principalSchema: "app",
                        principalTable: "nutrition_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_nutrition_plans_patient_profiles_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "routine_assignments",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    routine_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date = table.Column<DateTime>(type: "date", nullable: false),
                    end_date = table.Column<DateTime>(type: "date", nullable: true),
                    frequency = table.Column<int>(type: "integer", maxLength: 30, nullable: false),
                    status = table.Column<int>(type: "integer", maxLength: 20, nullable: false, defaultValue: 1),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_routine_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_routine_assignments_exercise_routines_routine_id",
                        column: x => x.routine_id,
                        principalSchema: "app",
                        principalTable: "exercise_routines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_routine_assignments_patient_profiles_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "routine_exercises",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    routine_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sets = table.Column<int>(type: "integer", nullable: true),
                    repetitions = table.Column<int>(type: "integer", nullable: true),
                    rest_seconds = table.Column<int>(type: "integer", nullable: true),
                    duration_secs = table.Column<int>(type: "integer", nullable: true),
                    weight_kg = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    media_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_routine_exercises", x => x.id);
                    table.ForeignKey(
                        name: "FK_routine_exercises_exercise_routines_routine_id",
                        column: x => x.routine_id,
                        principalSchema: "app",
                        principalTable: "exercise_routines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_routine_exercises_media_items_media_id",
                        column: x => x.media_id,
                        principalSchema: "app",
                        principalTable: "media_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "nutrition_plan_days",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_number = table.Column<int>(type: "integer", nullable: false),
                    meal_type = table.Column<int>(type: "integer", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    foods = table.Column<string>(type: "text", nullable: true),
                    calories = table.Column<int>(type: "integer", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    media_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nutrition_plan_days", x => x.id);
                    table.ForeignKey(
                        name: "FK_nutrition_plan_days_media_items_media_id",
                        column: x => x.media_id,
                        principalSchema: "app",
                        principalTable: "media_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_nutrition_plan_days_nutrition_plans_plan_id",
                        column: x => x.plan_id,
                        principalSchema: "app",
                        principalTable: "nutrition_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_exercise_routines_category",
                schema: "app",
                table: "exercise_routines",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "IX_exercise_routines_media_id",
                schema: "app",
                table: "exercise_routines",
                column: "media_id");

            migrationBuilder.CreateIndex(
                name: "ix_exercise_routines_status",
                schema: "app",
                table: "exercise_routines",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_nutrition_plan_days_media_id",
                schema: "app",
                table: "nutrition_plan_days",
                column: "media_id");

            migrationBuilder.CreateIndex(
                name: "ix_nutrition_plan_days_plan_day_meal",
                schema: "app",
                table: "nutrition_plan_days",
                columns: new[] { "plan_id", "day_number", "meal_type" });

            migrationBuilder.CreateIndex(
                name: "ix_nutrition_plans_is_template",
                schema: "app",
                table: "nutrition_plans",
                column: "is_template");

            migrationBuilder.CreateIndex(
                name: "ix_nutrition_plans_patient_id",
                schema: "app",
                table: "nutrition_plans",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_nutrition_plans_source_plan_id",
                schema: "app",
                table: "nutrition_plans",
                column: "source_plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_nutrition_plans_status",
                schema: "app",
                table: "nutrition_plans",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_routine_assignments_patient_id",
                schema: "app",
                table: "routine_assignments",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_routine_assignments_patient_status",
                schema: "app",
                table: "routine_assignments",
                columns: new[] { "patient_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_routine_assignments_routine_id",
                schema: "app",
                table: "routine_assignments",
                column: "routine_id");

            migrationBuilder.CreateIndex(
                name: "ix_routine_assignments_status",
                schema: "app",
                table: "routine_assignments",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_routine_exercises_media_id",
                schema: "app",
                table: "routine_exercises",
                column: "media_id");

            migrationBuilder.CreateIndex(
                name: "ix_routine_exercises_routine_sort",
                schema: "app",
                table: "routine_exercises",
                columns: new[] { "routine_id", "sort_order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "nutrition_plan_days",
                schema: "app");

            migrationBuilder.DropTable(
                name: "routine_assignments",
                schema: "app");

            migrationBuilder.DropTable(
                name: "routine_exercises",
                schema: "app");

            migrationBuilder.DropTable(
                name: "nutrition_plans",
                schema: "app");

            migrationBuilder.DropTable(
                name: "exercise_routines",
                schema: "app");
        }
    }
}
