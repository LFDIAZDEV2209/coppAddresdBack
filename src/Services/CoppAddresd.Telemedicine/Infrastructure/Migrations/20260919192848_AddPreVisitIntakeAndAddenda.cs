using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Telemedicine.Infrastructure.Migrations
{
    /// <summary>
    /// F4 — «Clínica y cumplimiento» (backend): pre-consulta del paciente
    /// (<c>tele.pre_visit_intakes</c>, 1:1 con la cita, PHI) y adendas del
    /// encuentro (<c>tele.encounter_addenda</c>, append-only, PHI). Adjunta el
    /// trigger de auditoría del backend a ambas tablas de forma CONDICIONAL e
    /// idempotente (mismo patrón que <c>AttachClinicalEncounterAudit</c>): si el
    /// schema <c>audit</c> no existe (base nueva de tests del microservicio), se
    /// omite sin error; la infraestructura de auditoría vive en el backend.
    /// </summary>
    public partial class AddPreVisitIntakeAndAddenda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "encounter_addenda",
                schema: "tele",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    encounter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_encounter_addenda", x => x.id);
                    table.ForeignKey(
                        name: "fk_encounter_addenda_encounters_encounter_id",
                        column: x => x.encounter_id,
                        principalSchema: "tele",
                        principalTable: "clinical_encounters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pre_visit_intakes",
                schema: "tele",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    symptoms = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    allergies = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    medications = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pre_visit_intakes", x => x.id);
                    table.ForeignKey(
                        name: "fk_pre_visit_intakes_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalSchema: "tele",
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_encounter_addenda_encounter_id_created_at",
                schema: "tele",
                table: "encounter_addenda",
                columns: new[] { "encounter_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_pre_visit_intakes_appointment_id",
                schema: "tele",
                table: "pre_visit_intakes",
                column: "appointment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pre_visit_intakes_patient_id",
                schema: "tele",
                table: "pre_visit_intakes",
                column: "patient_id");

            // Auditoría (F4): pre-consulta y adendas son PHI. Trigger condicional
            // (schema audit del backend) e idempotente para reintentos de deploy.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM pg_proc p
                        JOIN pg_namespace n ON n.oid = p.pronamespace
                        WHERE n.nspname = 'audit' AND p.proname = 'attach_table_audit')
                       AND NOT EXISTS (
                        SELECT 1 FROM pg_trigger
                        WHERE tgname = 'pre_visit_intakes_audit'
                          AND tgrelid = 'tele.pre_visit_intakes'::regclass)
                    THEN
                        PERFORM audit.attach_table_audit(
                            'tele', 'pre_visit_intakes', 'id', VARIADIC ARRAY[]::text[]);
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM pg_proc p
                        JOIN pg_namespace n ON n.oid = p.pronamespace
                        WHERE n.nspname = 'audit' AND p.proname = 'attach_table_audit')
                       AND NOT EXISTS (
                        SELECT 1 FROM pg_trigger
                        WHERE tgname = 'encounter_addenda_audit'
                          AND tgrelid = 'tele.encounter_addenda'::regclass)
                    THEN
                        PERFORM audit.attach_table_audit(
                            'tele', 'encounter_addenda', 'id', VARIADIC ARRAY[]::text[]);
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // El trigger cae con la tabla; se elimina explícito como el patrón
            // del encuentro clínico (por si la tabla se recreara sin migración).
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS pre_visit_intakes_audit ON tele.pre_visit_intakes;
                DROP TRIGGER IF EXISTS encounter_addenda_audit ON tele.encounter_addenda;
                """);

            migrationBuilder.DropTable(
                name: "encounter_addenda",
                schema: "tele");

            migrationBuilder.DropTable(
                name: "pre_visit_intakes",
                schema: "tele");
        }
    }
}
