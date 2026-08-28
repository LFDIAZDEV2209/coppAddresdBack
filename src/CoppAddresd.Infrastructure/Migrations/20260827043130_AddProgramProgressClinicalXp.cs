using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramProgressClinicalXp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "validated_at",
                schema: "app",
                table: "xp_ledger",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "validated_by",
                schema: "app",
                table: "xp_ledger",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "clinical_xp_reviews",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    health_score_id = table.Column<Guid>(type: "uuid", nullable: false),
                    metric_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, defaultValue: "CLINICAL_SIGNIFICANT"),
                    delta_pct = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clinical_xp_reviews", x => x.id);
                    table.CheckConstraint("ck_clinical_xp_reviews_status_values", "\"status\" IN ('pending', 'approved', 'rejected')");
                    table.ForeignKey(
                        name: "FK_clinical_xp_reviews_health_scores_health_score_id",
                        column: x => x.health_score_id,
                        principalSchema: "app",
                        principalTable: "health_scores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_clinical_xp_reviews_measurement_metrics_metric_id",
                        column: x => x.metric_id,
                        principalSchema: "app",
                        principalTable: "measurement_metrics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_clinical_xp_reviews_patient_profiles_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_clinical_xp_reviews_decided_by",
                schema: "app",
                table: "clinical_xp_reviews",
                column: "decided_by");

            migrationBuilder.CreateIndex(
                name: "IX_clinical_xp_reviews_health_score_id",
                schema: "app",
                table: "clinical_xp_reviews",
                column: "health_score_id");

            migrationBuilder.CreateIndex(
                name: "IX_clinical_xp_reviews_metric_id",
                schema: "app",
                table: "clinical_xp_reviews",
                column: "metric_id");

            migrationBuilder.CreateIndex(
                name: "ix_clinical_xp_reviews_status",
                schema: "app",
                table: "clinical_xp_reviews",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "uq_clinical_xp_reviews_patient_score_metric",
                schema: "app",
                table: "clinical_xp_reviews",
                columns: new[] { "patient_id", "health_score_id", "metric_id" },
                unique: true);

            // FKs hacia auth.users (creadas por SQL, patrón user_id): la tabla
            // "Users" vive en el schema auth y la gestiona el Auth Service
            // (otro DbContext). Actores nullable → ON DELETE SET NULL (preserva
            // la trazabilidad de la auditoría, precedente de AddProgramProgressCore):
            // xp_ledger.validated_by (quién validó el otorgamiento, SPEC §15) y
            // clinical_xp_reviews.decided_by (clínico que decidió la revisión).
            migrationBuilder.Sql("""
                ALTER TABLE app.xp_ledger
                    ADD CONSTRAINT fk_xp_ledger_validated_by
                    FOREIGN KEY (validated_by) REFERENCES auth."Users" ("Id")
                    ON DELETE SET NULL;

                ALTER TABLE app.clinical_xp_reviews
                    ADD CONSTRAINT fk_clinical_xp_reviews_decided_by
                    FOREIGN KEY (decided_by) REFERENCES auth."Users" ("Id")
                    ON DELETE SET NULL;
                """);

            // Auditoría trigger-based (SPEC §8.5): la tabla del módulo se cubre
            // explícitamente (el trigger no es automático para tablas nuevas).
            // clinical_xp_reviews no contiene PHI (solo ids, |Δ%| y estado): el
            // DML del clínico (decisión de revisión) queda auditado como el
            // resto del módulo.
            migrationBuilder.Sql("SELECT audit.attach_table_audit('app', 'clinical_xp_reviews', 'id', VARIADIC ARRAY[]::text[]);");

            // Permisos mínimos para el rol de la aplicación (convención app_user).
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON app.clinical_xp_reviews TO app_user;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS clinical_xp_reviews_audit ON app.clinical_xp_reviews;");

            migrationBuilder.Sql("""
                ALTER TABLE app.clinical_xp_reviews
                    DROP CONSTRAINT IF EXISTS fk_clinical_xp_reviews_decided_by;

                ALTER TABLE app.xp_ledger
                    DROP CONSTRAINT IF EXISTS fk_xp_ledger_validated_by;
                """);

            migrationBuilder.DropTable(
                name: "clinical_xp_reviews",
                schema: "app");

            migrationBuilder.DropColumn(
                name: "validated_at",
                schema: "app",
                table: "xp_ledger");

            migrationBuilder.DropColumn(
                name: "validated_by",
                schema: "app",
                table: "xp_ledger");
        }
    }
}
