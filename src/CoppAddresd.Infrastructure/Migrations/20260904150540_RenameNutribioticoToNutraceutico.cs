using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameNutribioticoToNutraceutico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- task_completions ---
            // Wire value stored as varchar via HasConversion<string>() on TaskCode.
            migrationBuilder.Sql(
                "UPDATE app.task_completions SET task_code = 'nutraceutico' WHERE task_code = 'nutribiotico';");

            // --- weekly_day_templates ---
            // Same varchar wire value.
            migrationBuilder.Sql(
                "UPDATE app.weekly_day_templates SET task_code = 'nutraceutico' WHERE task_code = 'nutribiotico';");

            // --- program_templates.essential_task_codes (jsonb array of strings) ---
            migrationBuilder.Sql(
                "UPDATE app.program_templates SET essential_task_codes = REPLACE(essential_task_codes::text, 'nutribiotico', 'nutraceutico')::jsonb WHERE essential_task_codes::text LIKE '%nutribiotico%';");

            // --- program_weeks.tasks_snapshot (jsonb con task_code por tarea diaria) ---
            migrationBuilder.Sql(
                "UPDATE app.program_weeks SET tasks_snapshot = REPLACE(tasks_snapshot::text, '\"nutribiotico\"', '\"nutraceutico\"')::jsonb WHERE tasks_snapshot::text LIKE '%\"nutribiotico\"%';");

            // --- xp_rules (catalog rename) ---
            // FK app.xp_ledger(rule_code) → app.xp_rules(code) is RESTRICT, so direct UPDATE violates FK.
            // Insert new row, move ledger, delete old.
            migrationBuilder.Sql(
                @"INSERT INTO app.xp_rules (id, code, name, category, base_xp, multiplier, max_per_day, max_per_week, requires_validation, active, valid_from, valid_until, created_at, updated_at)
                  SELECT gen_random_uuid(), 'TASK_NUTRACEUTICO', REPLACE(name, 'nutribiótico', 'nutracéutico'), category, base_xp, multiplier, max_per_day, max_per_week, requires_validation, active, valid_from, valid_until, now(), now()
                  FROM app.xp_rules WHERE code = 'TASK_NUTRIBIOTICO' AND NOT EXISTS (SELECT 1 FROM app.xp_rules WHERE code='TASK_NUTRACEUTICO');");

            // --- xp_ledger.rule_code ---
            migrationBuilder.Sql(
                "UPDATE app.xp_ledger SET rule_code = 'TASK_NUTRACEUTICO' WHERE rule_code = 'TASK_NUTRIBIOTICO';");

            // --- xp_ledger.reason ---
            migrationBuilder.Sql(
                "UPDATE app.xp_ledger SET reason = 'TASK_NUTRACEUTICO' WHERE reason = 'TASK_NUTRIBIOTICO';");

            migrationBuilder.Sql("DELETE FROM app.xp_rules WHERE code = 'TASK_NUTRIBIOTICO';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"INSERT INTO app.xp_rules (id, code, name, category, base_xp, multiplier, max_per_day, max_per_week, requires_validation, active, valid_from, valid_until, created_at, updated_at)
                  SELECT gen_random_uuid(), 'TASK_NUTRIBIOTICO', REPLACE(name, 'nutracéutico', 'nutribiótico'), category, base_xp, multiplier, max_per_day, max_per_week, requires_validation, active, valid_from, valid_until, now(), now()
                  FROM app.xp_rules WHERE code = 'TASK_NUTRACEUTICO' AND NOT EXISTS (SELECT 1 FROM app.xp_rules WHERE code='TASK_NUTRIBIOTICO');");

            migrationBuilder.Sql(
                "UPDATE app.xp_ledger SET reason = 'TASK_NUTRIBIOTICO' WHERE reason = 'TASK_NUTRACEUTICO';");

            migrationBuilder.Sql(
                "UPDATE app.xp_ledger SET rule_code = 'TASK_NUTRIBIOTICO' WHERE rule_code = 'TASK_NUTRACEUTICO';");

            migrationBuilder.Sql("DELETE FROM app.xp_rules WHERE code = 'TASK_NUTRACEUTICO';");

            migrationBuilder.Sql(
                "UPDATE app.program_templates SET essential_task_codes = REPLACE(essential_task_codes::text, 'nutraceutico', 'nutribiotico')::jsonb WHERE essential_task_codes::text LIKE '%nutraceutico%';");

            migrationBuilder.Sql(
                "UPDATE app.program_weeks SET tasks_snapshot = REPLACE(tasks_snapshot::text, '\"nutraceutico\"', '\"nutribiotico\"')::jsonb WHERE tasks_snapshot::text LIKE '%\"nutraceutico\"%';");

            migrationBuilder.Sql(
                "UPDATE app.weekly_day_templates SET task_code = 'nutribiotico' WHERE task_code = 'nutraceutico';");

            migrationBuilder.Sql(
                "UPDATE app.task_completions SET task_code = 'nutribiotico' WHERE task_code = 'nutraceutico';");
        }
    }
}
