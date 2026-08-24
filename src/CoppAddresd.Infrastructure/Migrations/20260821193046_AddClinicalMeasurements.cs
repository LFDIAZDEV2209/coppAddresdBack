using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicalMeasurements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "encounters",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    professional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "planned"),
                    started_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    ended_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_encounters", x => x.id);
                    table.ForeignKey(
                        name: "FK_encounters_patient_profiles_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_encounters_professionals_professional_id",
                        column: x => x.professional_id,
                        principalSchema: "erp",
                        principalTable: "professionals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "unit_of_measures",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unit_of_measures", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "measurement_metrics",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    default_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_measurement_metrics", x => x.id);
                    table.ForeignKey(
                        name: "FK_measurement_metrics_unit_of_measures_default_unit_id",
                        column: x => x.default_unit_id,
                        principalSchema: "app",
                        principalTable: "unit_of_measures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "clinical_measurements",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    metric_id = table.Column<Guid>(type: "uuid", nullable: false),
                    encounter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    value = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    observed_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clinical_measurements", x => x.id);
                    table.ForeignKey(
                        name: "FK_clinical_measurements_encounters_encounter_id",
                        column: x => x.encounter_id,
                        principalSchema: "app",
                        principalTable: "encounters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_clinical_measurements_measurement_metrics_metric_id",
                        column: x => x.metric_id,
                        principalSchema: "app",
                        principalTable: "measurement_metrics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_clinical_measurements_patient_profiles_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_clinical_measurements_unit_of_measures_unit_id",
                        column: x => x.unit_id,
                        principalSchema: "app",
                        principalTable: "unit_of_measures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "measurement_reference_ranges",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    metric_id = table.Column<Guid>(type: "uuid", nullable: false),
                    age_min = table.Column<int>(type: "integer", nullable: true),
                    age_max = table.Column<int>(type: "integer", nullable: true),
                    gender = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    min_value = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    max_value = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    notes = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_measurement_reference_ranges", x => x.id);
                    table.ForeignKey(
                        name: "FK_measurement_reference_ranges_measurement_metrics_metric_id",
                        column: x => x.metric_id,
                        principalSchema: "app",
                        principalTable: "measurement_metrics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_measurement_reference_ranges_unit_of_measures_unit_id",
                        column: x => x.unit_id,
                        principalSchema: "app",
                        principalTable: "unit_of_measures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_clinical_measurements_encounter_id",
                schema: "app",
                table: "clinical_measurements",
                column: "encounter_id");

            migrationBuilder.CreateIndex(
                name: "ix_clinical_measurements_metric_id",
                schema: "app",
                table: "clinical_measurements",
                column: "metric_id");

            migrationBuilder.CreateIndex(
                name: "ix_clinical_measurements_patient_id",
                schema: "app",
                table: "clinical_measurements",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_clinical_measurements_patient_observed",
                schema: "app",
                table: "clinical_measurements",
                columns: new[] { "patient_id", "observed_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_clinical_measurements_unit_id",
                schema: "app",
                table: "clinical_measurements",
                column: "unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_encounters_patient_id",
                schema: "app",
                table: "encounters",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_encounters_professional_id",
                schema: "app",
                table: "encounters",
                column: "professional_id");

            migrationBuilder.CreateIndex(
                name: "ix_encounters_status",
                schema: "app",
                table: "encounters",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_encounters_type",
                schema: "app",
                table: "encounters",
                column: "type");

            migrationBuilder.CreateIndex(
                name: "ix_measurement_metrics_code",
                schema: "app",
                table: "measurement_metrics",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_measurement_metrics_default_unit_id",
                schema: "app",
                table: "measurement_metrics",
                column: "default_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_measurement_reference_ranges_metric_id",
                schema: "app",
                table: "measurement_reference_ranges",
                column: "metric_id");

            migrationBuilder.CreateIndex(
                name: "ix_measurement_reference_ranges_unit_id",
                schema: "app",
                table: "measurement_reference_ranges",
                column: "unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_unit_of_measures_code",
                schema: "app",
                table: "unit_of_measures",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clinical_measurements",
                schema: "app");

            migrationBuilder.DropTable(
                name: "measurement_reference_ranges",
                schema: "app");

            migrationBuilder.DropTable(
                name: "encounters",
                schema: "app");

            migrationBuilder.DropTable(
                name: "measurement_metrics",
                schema: "app");

            migrationBuilder.DropTable(
                name: "unit_of_measures",
                schema: "app");
        }
    }
}
