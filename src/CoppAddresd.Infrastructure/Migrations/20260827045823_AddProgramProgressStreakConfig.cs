using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramProgressStreakConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "essential_task_codes",
                schema: "app",
                table: "program_templates",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<short>(
                name: "streak_min_tasks",
                schema: "app",
                table: "program_templates",
                type: "smallint",
                nullable: false,
                defaultValue: (short)1);

            migrationBuilder.AddCheckConstraint(
                name: "ck_program_templates_streak_min_tasks_positive",
                schema: "app",
                table: "program_templates",
                sql: "\"streak_min_tasks\" >= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_program_templates_streak_min_tasks_positive",
                schema: "app",
                table: "program_templates");

            migrationBuilder.DropColumn(
                name: "essential_task_codes",
                schema: "app",
                table: "program_templates");

            migrationBuilder.DropColumn(
                name: "streak_min_tasks",
                schema: "app",
                table: "program_templates");
        }
    }
}
