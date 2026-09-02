using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyDayTemplateContentLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "nutrition_plan_id",
                schema: "app",
                table: "weekly_day_templates",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "routine_id",
                schema: "app",
                table: "weekly_day_templates",
                type: "uuid",
                nullable: true
            );

            // FKs por SQL (patrón weaknesses/assigned_to): las tablas de wellness
            // viven en el mismo DbContext pero sin navegaciones en el modelo;
            // Restrict para preservar la fila del día si el plan/rutina se elimina.
            migrationBuilder.Sql(
                """
                ALTER TABLE app.weekly_day_templates
                    ADD CONSTRAINT fk_weekly_day_templates_routine_id
                    FOREIGN KEY (routine_id) REFERENCES app.exercise_routines (id)
                    ON DELETE RESTRICT;
                ALTER TABLE app.weekly_day_templates
                    ADD CONSTRAINT fk_weekly_day_templates_nutrition_plan_id
                    FOREIGN KEY (nutrition_plan_id) REFERENCES app.nutrition_plans (id)
                    ON DELETE RESTRICT;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE app.weekly_day_templates
                    DROP CONSTRAINT IF EXISTS fk_weekly_day_templates_routine_id;
                ALTER TABLE app.weekly_day_templates
                    DROP CONSTRAINT IF EXISTS fk_weekly_day_templates_nutrition_plan_id;
                """
            );

            migrationBuilder.DropColumn(
                name: "routine_id",
                schema: "app",
                table: "weekly_day_templates"
            );

            migrationBuilder.DropColumn(
                name: "nutrition_plan_id",
                schema: "app",
                table: "weekly_day_templates"
            );
        }
    }
}
