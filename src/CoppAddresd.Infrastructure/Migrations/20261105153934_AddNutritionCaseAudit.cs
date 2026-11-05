// Migracion aditiva: conserva tablas y datos legacy; defaults JSON validos para filas previas.
// Rollback recomendado: volver al binario anterior conservando columnas y bitacora.
// Down solo para entorno sin datos nuevos o tras respaldo y autorizacion del owner.
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNutritionCaseAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "clinical_profile_json",
                schema: "app",
                table: "nutrition_prescriptions",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<bool>(
                name: "manual_edits_confirmed",
                schema: "app",
                table: "nutrition_prescriptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "missing_data_json",
                schema: "app",
                table: "nutrition_prescriptions",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "narrative_source",
                schema: "app",
                table: "nutrition_prescriptions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "deterministic_fallback");

            migrationBuilder.AddColumn<string>(
                name: "targets_json",
                schema: "app",
                table: "nutrition_prescriptions",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.CreateTable(
                name: "nutrition_prescription_events",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    prescription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    critical_count = table.Column<int>(type: "integer", nullable: false),
                    critical_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    auto_approved = table.Column<bool>(type: "boolean", nullable: false),
                    snapshot_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nutrition_prescription_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_nutrition_prescription_events_nutrition_prescriptions_presc~",
                        column: x => x.prescription_id,
                        principalSchema: "app",
                        principalTable: "nutrition_prescriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_nutrition_prescription_events_history",
                schema: "app",
                table: "nutrition_prescription_events",
                columns: new[] { "prescription_id", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "nutrition_prescription_events",
                schema: "app");

            migrationBuilder.DropColumn(
                name: "clinical_profile_json",
                schema: "app",
                table: "nutrition_prescriptions");

            migrationBuilder.DropColumn(
                name: "manual_edits_confirmed",
                schema: "app",
                table: "nutrition_prescriptions");

            migrationBuilder.DropColumn(
                name: "missing_data_json",
                schema: "app",
                table: "nutrition_prescriptions");

            migrationBuilder.DropColumn(
                name: "narrative_source",
                schema: "app",
                table: "nutrition_prescriptions");

            migrationBuilder.DropColumn(
                name: "targets_json",
                schema: "app",
                table: "nutrition_prescriptions");
        }
    }
}
