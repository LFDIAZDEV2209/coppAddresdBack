using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramProgressScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clinical_baselines",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    metric_id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    favorable_direction = table.Column<short>(type: "smallint", nullable: false),
                    target_value = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: true),
                    measured_at = table.Column<DateOnly>(type: "date", nullable: false),
                    set_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clinical_baselines", x => x.id);
                    table.CheckConstraint("ck_clinical_baselines_favorable_direction", "\"favorable_direction\" IN (-1, 1)");
                    table.ForeignKey(
                        name: "FK_clinical_baselines_measurement_metrics_metric_id",
                        column: x => x.metric_id,
                        principalSchema: "app",
                        principalTable: "measurement_metrics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_clinical_baselines_patient_profiles_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_clinical_baselines_unit_of_measures_unit_id",
                        column: x => x.unit_id,
                        principalSchema: "app",
                        principalTable: "unit_of_measures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "health_score_weights",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    dimension = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    weight = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_score_weights", x => x.id);
                    table.CheckConstraint("ck_health_score_weights_weight_range", "\"weight\" >= 0 AND \"weight\" <= 1");
                });

            migrationBuilder.CreateTable(
                name: "health_scores",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    score_previous = table.Column<int>(type: "integer", nullable: true),
                    score_adherence = table.Column<int>(type: "integer", nullable: false),
                    score_clinical = table.Column<int>(type: "integer", nullable: false),
                    score_nutrition = table.Column<int>(type: "integer", nullable: false),
                    score_psychology = table.Column<int>(type: "integer", nullable: false),
                    score_exercise = table.Column<int>(type: "integer", nullable: false),
                    trend = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    calculated_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_scores", x => x.id);
                    table.CheckConstraint("ck_health_scores_score_adherence_range", "\"score_adherence\" BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_health_scores_score_clinical_range", "\"score_clinical\" BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_health_scores_score_exercise_range", "\"score_exercise\" BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_health_scores_score_nutrition_range", "\"score_nutrition\" BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_health_scores_score_previous_range", "\"score_previous\" BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_health_scores_score_psychology_range", "\"score_psychology\" BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_health_scores_score_range", "\"score\" BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_health_scores_trend_values", "\"trend\" IN ('up','down','stable')");
                    table.ForeignKey(
                        name: "FK_health_scores_patient_profiles_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transformation_scores",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    score_previous = table.Column<int>(type: "integer", nullable: true),
                    week_number = table.Column<int>(type: "integer", nullable: false),
                    detail = table.Column<JsonElement>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    overall_trend = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    calculated_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transformation_scores", x => x.id);
                    table.CheckConstraint("ck_transformation_scores_overall_trend_values", "\"overall_trend\" IN ('up','down','stable')");
                    table.CheckConstraint("ck_transformation_scores_score_previous_range", "\"score_previous\" BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_transformation_scores_score_range", "\"score\" BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_transformation_scores_week_number_range", "\"week_number\" >= 1");
                    table.ForeignKey(
                        name: "FK_transformation_scores_patient_profiles_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_clinical_baselines_metric_id",
                schema: "app",
                table: "clinical_baselines",
                column: "metric_id");

            migrationBuilder.CreateIndex(
                name: "ix_clinical_baselines_patient_id",
                schema: "app",
                table: "clinical_baselines",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_clinical_baselines_set_by",
                schema: "app",
                table: "clinical_baselines",
                column: "set_by");

            migrationBuilder.CreateIndex(
                name: "IX_clinical_baselines_unit_id",
                schema: "app",
                table: "clinical_baselines",
                column: "unit_id");

            migrationBuilder.CreateIndex(
                name: "uq_clinical_baselines_patient_metric",
                schema: "app",
                table: "clinical_baselines",
                columns: new[] { "patient_id", "metric_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_health_score_weights_dimension",
                schema: "app",
                table: "health_score_weights",
                column: "dimension",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_health_scores_patient_id_period_end",
                schema: "app",
                table: "health_scores",
                columns: new[] { "patient_id", "period_end" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "uq_health_scores_patient_period",
                schema: "app",
                table: "health_scores",
                columns: new[] { "patient_id", "period_start", "period_end" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transformation_scores_patient_week",
                schema: "app",
                table: "transformation_scores",
                columns: new[] { "patient_id", "week_number" },
                descending: new[] { false, true });

            // FKs hacia auth.users (creadas por SQL, patrón user_id): la tabla
            // "Users" vive en el schema auth y la gestiona el Auth Service
            // (otro DbContext). Los actores nullable (created_by/updated_by)
            // usan ON DELETE SET NULL (preserva la auditoría, precedente de
            // AddProgramProgressCore). clinical_baselines.set_by es NOT NULL
            // (autoría clínica obligatoria, AC-22) y por tanto usa ON DELETE
            // RESTRICT: un clínico cuyo usuario se elimine debe conservar sus
            // líneas base, nunca quedar con set_by nulo.
            migrationBuilder.Sql("""
                ALTER TABLE app.health_score_weights
                    ADD CONSTRAINT fk_health_score_weights_created_by
                    FOREIGN KEY (created_by) REFERENCES auth."Users" ("Id")
                    ON DELETE SET NULL;

                ALTER TABLE app.health_score_weights
                    ADD CONSTRAINT fk_health_score_weights_updated_by
                    FOREIGN KEY (updated_by) REFERENCES auth."Users" ("Id")
                    ON DELETE SET NULL;

                ALTER TABLE app.clinical_baselines
                    ADD CONSTRAINT fk_clinical_baselines_set_by
                    FOREIGN KEY (set_by) REFERENCES auth."Users" ("Id")
                    ON DELETE RESTRICT;
                """);

            // Permisos mínimos para el rol de la aplicación (convención app_user).
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON app.health_score_weights TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON app.clinical_baselines TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON app.health_scores TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON app.transformation_scores TO app_user;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clinical_baselines",
                schema: "app");

            migrationBuilder.DropTable(
                name: "health_score_weights",
                schema: "app");

            migrationBuilder.DropTable(
                name: "health_scores",
                schema: "app");

            migrationBuilder.DropTable(
                name: "transformation_scores",
                schema: "app");
        }
    }
}
