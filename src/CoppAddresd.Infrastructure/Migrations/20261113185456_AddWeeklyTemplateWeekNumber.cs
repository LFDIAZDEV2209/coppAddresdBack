using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations;

/// <summary>Amplía el catálogo sin rellenar ni reescribir filas existentes.</summary>
public partial class AddWeeklyTemplateWeekNumber : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "week_number", schema: "app",
            table: "weekly_day_templates", type: "integer", nullable: true);
        // NOT VALID evita recorrer la tabla bajo un bloqueo exclusivo prolongado.
        migrationBuilder.Sql("ALTER TABLE app.weekly_day_templates ADD CONSTRAINT ck_weekly_day_templates_week_number CHECK (week_number IS NULL OR week_number >= 1) NOT VALID;");
        migrationBuilder.Sql("ALTER TABLE app.weekly_day_templates VALIDATE CONSTRAINT ck_weekly_day_templates_week_number;");
        // Primero se establece la nueva protección; después se retira la anterior.
        // Si falla CONCURRENTLY, retirar el índice inválido antes de reintentar.
        migrationBuilder.Sql("CREATE UNIQUE INDEX CONCURRENTLY uq_weekly_day_templates_template_week_day_task ON app.weekly_day_templates (template_id, COALESCE(week_number, 0), weekday, task_code);", suppressTransaction: true);
        migrationBuilder.Sql("DROP INDEX CONCURRENTLY app.uq_weekly_day_templates_template_weekday_task;", suppressTransaction: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Reversa: exportar y retirar explícitamente las filas específicas antes de
        // bajar de versión. Se aborta en vez de perder datos o colisionar con genéricas.
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM app.weekly_day_templates WHERE week_number IS NOT NULL) THEN
                    RAISE EXCEPTION 'Reversa requiere exportar y retirar las filas de semanas específicas';
                END IF;
            END $$;
            """);
        migrationBuilder.Sql("CREATE UNIQUE INDEX CONCURRENTLY uq_weekly_day_templates_template_weekday_task ON app.weekly_day_templates (template_id, weekday, task_code);", suppressTransaction: true);
        migrationBuilder.Sql("DROP INDEX CONCURRENTLY app.uq_weekly_day_templates_template_week_day_task;", suppressTransaction: true);
        migrationBuilder.DropCheckConstraint(name: "ck_weekly_day_templates_week_number", schema: "app", table: "weekly_day_templates");
        migrationBuilder.DropColumn(name: "week_number", schema: "app", table: "weekly_day_templates");
    }
}
