using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramProgressWeaknesses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "weaknesses",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "low"),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    detected_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    metric_id = table.Column<Guid>(type: "uuid", nullable: true),
                    indicator_value = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: true),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "ai"),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "open"),
                    assigned_to = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weaknesses", x => x.id);
                    table.CheckConstraint("ck_weaknesses_category_values", "\"category\" IN ('nutritional', 'clinical', 'psychological', 'exercise', 'adherence', 'supplement', 'sleep', 'motivation')");
                    table.CheckConstraint("ck_weaknesses_severity_values", "\"severity\" IN ('low', 'medium', 'high', 'critical')");
                    table.CheckConstraint("ck_weaknesses_source_values", "\"source\" IN ('ai', 'professional', 'system')");
                    table.CheckConstraint("ck_weaknesses_status_values", "\"status\" IN ('open', 'acknowledged', 'in_intervention', 'resolved', 'dismissed')");
                    table.ForeignKey(
                        name: "FK_weaknesses_measurement_metrics_metric_id",
                        column: x => x.metric_id,
                        principalSchema: "app",
                        principalTable: "measurement_metrics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_weaknesses_patient_profiles_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_weaknesses_metric_id",
                schema: "app",
                table: "weaknesses",
                column: "metric_id");

            migrationBuilder.CreateIndex(
                name: "ix_weaknesses_patient_status",
                schema: "app",
                table: "weaknesses",
                columns: new[] { "patient_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_weaknesses_status",
                schema: "app",
                table: "weaknesses",
                column: "status");

            // FK hacia auth.users (creada por SQL, patrón user_id): la tabla
            // "Users" vive en el schema auth y la gestiona el Auth Service (otro
            // DbContext). assigned_to es el clínico asignado al caso → nullable
            // con ON DELETE SET NULL (preserva la trazabilidad, precedente de
            // AddProgramProgressClinicalXp).
            migrationBuilder.Sql("""
                ALTER TABLE app.weaknesses
                    ADD CONSTRAINT fk_weaknesses_assigned_to
                    FOREIGN KEY (assigned_to) REFERENCES auth."Users" ("Id")
                    ON DELETE SET NULL;
                """);

            // Permisos mínimos para el rol de la aplicación (convención app_user).
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON app.weaknesses TO app_user;
                """);

            // Auditoría: la tabla NO se adjunta al trigger de auditoría. A
            // diferencia de clinical_xp_reviews / habit_checks (sin PHI), el
            // campo description de las debilidades puede contener contexto
            // clínico del hallazgo (SPEC §21, A; misma exclusión por diseño que
            // emotional_records en SPEC §8.5): su DML nunca debe aterrizar en
            // audit.activity_logs.old_data/new_data.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE app.weaknesses
                    DROP CONSTRAINT IF EXISTS fk_weaknesses_assigned_to;
                """);

            migrationBuilder.DropTable(
                name: "weaknesses",
                schema: "app");
        }
    }
}
