using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameProgramMilestoneSendsToProgramControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_program_milestone_sends_program_enrollments_enrollment_id",
                schema: "app",
                table: "program_milestone_sends");

            migrationBuilder.DropPrimaryKey(
                name: "PK_program_milestone_sends",
                schema: "app",
                table: "program_milestone_sends");

            migrationBuilder.RenameTable(
                name: "program_milestone_sends",
                schema: "app",
                newName: "program_controls",
                newSchema: "app");

            // Auditoría trigger-based (SPEC §8.5): el trigger es por nombre de
            // tabla (<c>program_milestone_sends_audit</c>) y PostgreSQL NO lo
            // renombra junto con la tabla — se DROP y se re-adjunta con el
            // nombre nuevo para que el DML de la tabla siga auditado.
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS program_milestone_sends_audit ON app.program_controls;");

            migrationBuilder.RenameIndex(
                name: "ix_program_milestone_sends_status",
                schema: "app",
                table: "program_controls",
                newName: "ix_program_controls_status");

            migrationBuilder.RenameIndex(
                name: "ix_program_milestone_sends_enrollment_day",
                schema: "app",
                table: "program_controls",
                newName: "ix_program_controls_enrollment_day");

            migrationBuilder.AddColumn<string>(
                name: "closed_reason",
                schema: "app",
                table: "program_controls",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "completed_at",
                schema: "app",
                table: "program_controls",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "exam_batch_id",
                schema: "app",
                table: "program_controls",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "followup_sent_at",
                schema: "app",
                table: "program_controls",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "responded_at",
                schema: "app",
                table: "program_controls",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_program_controls",
                schema: "app",
                table: "program_controls",
                column: "id");

            migrationBuilder.AddCheckConstraint(
                name: "CK_program_controls_closed_reason",
                schema: "app",
                table: "program_controls",
                sql: "\"closed_reason\" IN ('declined', 'no_upload_timeout')");

            migrationBuilder.AddForeignKey(
                name: "FK_program_controls_program_enrollments_enrollment_id",
                schema: "app",
                table: "program_controls",
                column: "enrollment_id",
                principalSchema: "app",
                principalTable: "program_enrollments",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            // Auditoría trigger-based (SPEC §8.5): re-adjunta el trigger de
            // auditoría con el nombre de la tabla nueva (mismo precedente de
            // notifications / streak_states en la migración original).
            migrationBuilder.Sql("SELECT audit.attach_table_audit('app', 'program_controls', 'id', VARIADIC ARRAY[]::text[]);");

            // Permisos mínimos para el rol de la aplicación (convención
            // app_user): se re-emiten sobre el nombre nuevo (los GRANTs
            // sobreviven al rename por OID, pero se re-emiten para dejar la
            // convención explícita igual que la migración original).
            migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON app.program_controls TO app_user;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Auditoría trigger-based: DROP del trigger con nombre nuevo antes
            // de renombrar la tabla de vuelta (los triggers no se renombran
            // junto con la tabla).
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS program_controls_audit ON app.program_controls;");
            migrationBuilder.DropForeignKey(
                name: "FK_program_controls_program_enrollments_enrollment_id",
                schema: "app",
                table: "program_controls");

            migrationBuilder.DropPrimaryKey(
                name: "PK_program_controls",
                schema: "app",
                table: "program_controls");

            migrationBuilder.DropCheckConstraint(
                name: "CK_program_controls_closed_reason",
                schema: "app",
                table: "program_controls");

            migrationBuilder.DropColumn(
                name: "closed_reason",
                schema: "app",
                table: "program_controls");

            migrationBuilder.DropColumn(
                name: "completed_at",
                schema: "app",
                table: "program_controls");

            migrationBuilder.DropColumn(
                name: "exam_batch_id",
                schema: "app",
                table: "program_controls");

            migrationBuilder.DropColumn(
                name: "followup_sent_at",
                schema: "app",
                table: "program_controls");

            migrationBuilder.DropColumn(
                name: "responded_at",
                schema: "app",
                table: "program_controls");

            migrationBuilder.RenameTable(
                name: "program_controls",
                schema: "app",
                newName: "program_milestone_sends",
                newSchema: "app");

            migrationBuilder.RenameIndex(
                name: "ix_program_controls_status",
                schema: "app",
                table: "program_milestone_sends",
                newName: "ix_program_milestone_sends_status");

            migrationBuilder.RenameIndex(
                name: "ix_program_controls_enrollment_day",
                schema: "app",
                table: "program_milestone_sends",
                newName: "ix_program_milestone_sends_enrollment_day");

            migrationBuilder.AddPrimaryKey(
                name: "PK_program_milestone_sends",
                schema: "app",
                table: "program_milestone_sends",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_program_milestone_sends_program_enrollments_enrollment_id",
                schema: "app",
                table: "program_milestone_sends",
                column: "enrollment_id",
                principalSchema: "app",
                principalTable: "program_enrollments",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            // Restaura el trigger de auditoría con el nombre original.
            migrationBuilder.Sql("SELECT audit.attach_table_audit('app', 'program_milestone_sends', 'id', VARIADIC ARRAY[]::text[]);");
        }
    }
}
