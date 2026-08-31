using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramProgressSafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_adaptation_recommendations_program_enrollments_enrollment_id",
                schema: "app",
                table: "adaptation_recommendations");

            migrationBuilder.DropForeignKey(
                name: "FK_daily_checkins_program_enrollments_enrollment_id",
                schema: "app",
                table: "daily_checkins");

            migrationBuilder.DropForeignKey(
                name: "FK_emotional_records_program_enrollments_program_enrollment_id",
                schema: "app",
                table: "emotional_records");

            migrationBuilder.DropForeignKey(
                name: "FK_program_weeks_program_enrollments_enrollment_id",
                schema: "app",
                table: "program_weeks");

            migrationBuilder.DropForeignKey(
                name: "FK_streak_freezes_program_enrollments_enrollment_id",
                schema: "app",
                table: "streak_freezes");

            migrationBuilder.DropForeignKey(
                name: "FK_streak_states_program_enrollments_enrollment_id",
                schema: "app",
                table: "streak_states");

            migrationBuilder.DropForeignKey(
                name: "FK_task_completions_program_enrollments_enrollment_id",
                schema: "app",
                table: "task_completions");

            migrationBuilder.DropForeignKey(
                name: "FK_xp_ledger_program_enrollments_enrollment_id",
                schema: "app",
                table: "xp_ledger");

            migrationBuilder.AddColumn<Guid>(
                name: "media_id",
                schema: "app",
                table: "weekly_day_templates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "uq_program_enrollments_id_patient",
                schema: "app",
                table: "program_enrollments",
                columns: new[] { "id", "patient_id" });

            migrationBuilder.CreateIndex(
                name: "ix_weekly_day_templates_media_id",
                schema: "app",
                table: "weekly_day_templates",
                column: "media_id");

            migrationBuilder.CreateIndex(
                name: "IX_emotional_records_program_enrollment_id_patient_id",
                schema: "app",
                table: "emotional_records",
                columns: new[] { "program_enrollment_id", "patient_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_adaptation_recommendations_program_enrollments_enrollment_id",
                schema: "app",
                table: "adaptation_recommendations",
                column: "enrollment_id",
                principalSchema: "app",
                principalTable: "program_enrollments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_daily_checkins_program_enrollments_enrollment_id",
                schema: "app",
                table: "daily_checkins",
                column: "enrollment_id",
                principalSchema: "app",
                principalTable: "program_enrollments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_emotional_records_program_enrollments_program_enrollment_id~",
                schema: "app",
                table: "emotional_records",
                columns: new[] { "program_enrollment_id", "patient_id" },
                principalSchema: "app",
                principalTable: "program_enrollments",
                principalColumns: new[] { "id", "patient_id" },
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_program_weeks_program_enrollments_enrollment_id",
                schema: "app",
                table: "program_weeks",
                column: "enrollment_id",
                principalSchema: "app",
                principalTable: "program_enrollments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_streak_freezes_program_enrollments_enrollment_id",
                schema: "app",
                table: "streak_freezes",
                column: "enrollment_id",
                principalSchema: "app",
                principalTable: "program_enrollments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_streak_states_program_enrollments_enrollment_id",
                schema: "app",
                table: "streak_states",
                column: "enrollment_id",
                principalSchema: "app",
                principalTable: "program_enrollments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_task_completions_program_enrollments_enrollment_id",
                schema: "app",
                table: "task_completions",
                column: "enrollment_id",
                principalSchema: "app",
                principalTable: "program_enrollments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_weekly_day_templates_media_items_media_id",
                schema: "app",
                table: "weekly_day_templates",
                column: "media_id",
                principalSchema: "app",
                principalTable: "media_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_xp_ledger_program_enrollments_enrollment_id",
                schema: "app",
                table: "xp_ledger",
                column: "enrollment_id",
                principalSchema: "app",
                principalTable: "program_enrollments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Índice simple de lookup por patient_id (SPEC §3.3). EF Core
            // deduplica índices por set de columnas (no puede coexistir con
            // uq_program_enrollments_patient_active en el modelo), por lo que se
            // crea por SQL. IF NOT EXISTS: ya existe en BD vía la migración B1
            // (AddProgramProgressCore), aplicar es idempotente.
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS ix_program_enrollments_patient_id
                    ON app.program_enrollments USING btree (patient_id);
                """);

            // Auditoría trigger-based para las 4 tablas de rendición de cuentas
            // del módulo: program_enrollments, task_completions, xp_ledger y
            // adaptation_recommendations (mismo patrón que AddLegalDocumentsAudit).
            // Las tablas restantes del módulo NO se auditan a propósito para evitar
            // PHI (mood/barriers/notes de daily_checkins y emotional_records) en
            // activity_logs.old_data/new_data. De las tablas auditadas se excluyen
            // del jsonb las columnas voluminosas (reason/payload).
            migrationBuilder.Sql("SELECT audit.attach_table_audit('app', 'program_enrollments', 'id', VARIADIC ARRAY[]::text[]);");
            migrationBuilder.Sql("SELECT audit.attach_table_audit('app', 'task_completions', 'id', VARIADIC ARRAY[]::text[]);");
            migrationBuilder.Sql("SELECT audit.attach_table_audit('app', 'xp_ledger', 'id', VARIADIC ARRAY['reason']::text[]);");
            migrationBuilder.Sql("SELECT audit.attach_table_audit('app', 'adaptation_recommendations', 'id', VARIADIC ARRAY['payload', 'reason']::text[]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Triggers de auditoría adjuntados por esta migración. El índice
            // simple ix_program_enrollments_patient_id NO se elimina aquí:
            // pertenece a B1 (AddProgramProgressCore), que lo crea por SQL.
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS program_enrollments_audit ON app.program_enrollments;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS task_completions_audit ON app.task_completions;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS xp_ledger_audit ON app.xp_ledger;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS adaptation_recommendations_audit ON app.adaptation_recommendations;");

            migrationBuilder.DropForeignKey(
                name: "FK_adaptation_recommendations_program_enrollments_enrollment_id",
                schema: "app",
                table: "adaptation_recommendations");

            migrationBuilder.DropForeignKey(
                name: "FK_daily_checkins_program_enrollments_enrollment_id",
                schema: "app",
                table: "daily_checkins");

            migrationBuilder.DropForeignKey(
                name: "FK_emotional_records_program_enrollments_program_enrollment_id~",
                schema: "app",
                table: "emotional_records");

            migrationBuilder.DropForeignKey(
                name: "FK_program_weeks_program_enrollments_enrollment_id",
                schema: "app",
                table: "program_weeks");

            migrationBuilder.DropForeignKey(
                name: "FK_streak_freezes_program_enrollments_enrollment_id",
                schema: "app",
                table: "streak_freezes");

            migrationBuilder.DropForeignKey(
                name: "FK_streak_states_program_enrollments_enrollment_id",
                schema: "app",
                table: "streak_states");

            migrationBuilder.DropForeignKey(
                name: "FK_task_completions_program_enrollments_enrollment_id",
                schema: "app",
                table: "task_completions");

            migrationBuilder.DropForeignKey(
                name: "FK_weekly_day_templates_media_items_media_id",
                schema: "app",
                table: "weekly_day_templates");

            migrationBuilder.DropForeignKey(
                name: "FK_xp_ledger_program_enrollments_enrollment_id",
                schema: "app",
                table: "xp_ledger");

            migrationBuilder.DropIndex(
                name: "ix_weekly_day_templates_media_id",
                schema: "app",
                table: "weekly_day_templates");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_program_enrollments_id_patient",
                schema: "app",
                table: "program_enrollments");

            migrationBuilder.DropIndex(
                name: "IX_emotional_records_program_enrollment_id_patient_id",
                schema: "app",
                table: "emotional_records");

            migrationBuilder.DropColumn(
                name: "media_id",
                schema: "app",
                table: "weekly_day_templates");

            migrationBuilder.AddForeignKey(
                name: "FK_adaptation_recommendations_program_enrollments_enrollment_id",
                schema: "app",
                table: "adaptation_recommendations",
                column: "enrollment_id",
                principalSchema: "app",
                principalTable: "program_enrollments",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_daily_checkins_program_enrollments_enrollment_id",
                schema: "app",
                table: "daily_checkins",
                column: "enrollment_id",
                principalSchema: "app",
                principalTable: "program_enrollments",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_emotional_records_program_enrollments_program_enrollment_id",
                schema: "app",
                table: "emotional_records",
                column: "program_enrollment_id",
                principalSchema: "app",
                principalTable: "program_enrollments",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_program_weeks_program_enrollments_enrollment_id",
                schema: "app",
                table: "program_weeks",
                column: "enrollment_id",
                principalSchema: "app",
                principalTable: "program_enrollments",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_streak_freezes_program_enrollments_enrollment_id",
                schema: "app",
                table: "streak_freezes",
                column: "enrollment_id",
                principalSchema: "app",
                principalTable: "program_enrollments",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_streak_states_program_enrollments_enrollment_id",
                schema: "app",
                table: "streak_states",
                column: "enrollment_id",
                principalSchema: "app",
                principalTable: "program_enrollments",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_task_completions_program_enrollments_enrollment_id",
                schema: "app",
                table: "task_completions",
                column: "enrollment_id",
                principalSchema: "app",
                principalTable: "program_enrollments",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_xp_ledger_program_enrollments_enrollment_id",
                schema: "app",
                table: "xp_ledger",
                column: "enrollment_id",
                principalSchema: "app",
                principalTable: "program_enrollments",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
