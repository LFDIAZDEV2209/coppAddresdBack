using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramProgressAuditCoverage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // auditoría trigger-based para las 6 tablas restantes del módulo
            // (complementa AddProgramProgressSafety, que cubrió
            // program_enrollments, task_completions, xp_ledger y
            // adaptation_recommendations). Se EXCLUYE deliberadamente
            // emotional_records: mood_score/barriers/notes son PHI y nunca
            // deben aterrizar en activity_logs.old_data/new_data. De
            // daily_checkins los payloads jsonb excluyen mood_score/barriers
            // (notas solo existen en emotional_records, que no se audita).
            // Ojo: streak_states tiene PK enrollment_id (no id) — el trigger
            // deriva record_id de la columna PK (TG_ARGV[0]).
            migrationBuilder.Sql("SELECT audit.attach_table_audit('app', 'program_templates', 'id', VARIADIC ARRAY[]::text[]);");
            migrationBuilder.Sql("SELECT audit.attach_table_audit('app', 'weekly_day_templates', 'id', VARIADIC ARRAY[]::text[]);");
            migrationBuilder.Sql("SELECT audit.attach_table_audit('app', 'program_weeks', 'id', VARIADIC ARRAY[]::text[]);");
            migrationBuilder.Sql("SELECT audit.attach_table_audit('app', 'daily_checkins', 'id', VARIADIC ARRAY['mood_score', 'barriers']::text[]);");
            migrationBuilder.Sql("SELECT audit.attach_table_audit('app', 'streak_states', 'enrollment_id', VARIADIC ARRAY[]::text[]);");
            migrationBuilder.Sql("SELECT audit.attach_table_audit('app', 'streak_freezes', 'id', VARIADIC ARRAY[]::text[]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS program_templates_audit ON app.program_templates;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS weekly_day_templates_audit ON app.weekly_day_templates;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS program_weeks_audit ON app.program_weeks;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS daily_checkins_audit ON app.daily_checkins;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS streak_states_audit ON app.streak_states;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS streak_freezes_audit ON app.streak_freezes;");
        }
    }
}