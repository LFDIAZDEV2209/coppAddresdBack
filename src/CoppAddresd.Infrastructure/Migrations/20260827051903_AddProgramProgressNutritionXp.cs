using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramProgressNutritionXp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "habit_templates",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_habit_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "habit_checks",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    habit_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_done = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_habit_checks", x => x.id);
                    table.ForeignKey(
                        name: "FK_habit_checks_habit_templates_habit_template_id",
                        column: x => x.habit_template_id,
                        principalSchema: "app",
                        principalTable: "habit_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_habit_checks_patient_profiles_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_habit_checks_habit_template_id",
                schema: "app",
                table: "habit_checks",
                column: "habit_template_id");

            migrationBuilder.CreateIndex(
                name: "ix_habit_checks_local_date",
                schema: "app",
                table: "habit_checks",
                column: "local_date");

            migrationBuilder.CreateIndex(
                name: "ix_habit_checks_patient_id",
                schema: "app",
                table: "habit_checks",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "uq_habit_checks_patient_template_date",
                schema: "app",
                table: "habit_checks",
                columns: new[] { "patient_id", "habit_template_id", "local_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_habit_templates_category",
                schema: "app",
                table: "habit_templates",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "uq_habit_templates_code",
                schema: "app",
                table: "habit_templates",
                column: "code",
                unique: true);

            // Auditoría trigger-based (SPEC §8.5): las tablas del módulo se
            // cubren explícitamente (el trigger no es automático para tablas
            // nuevas). habit_checks NO contiene PHI (ids, código de comida y
            // fecha local — sin mood/notas), por lo que su DML queda auditado
            // como el resto del módulo (precedente: clinical_xp_reviews).
            migrationBuilder.Sql("SELECT audit.attach_table_audit('app', 'habit_templates', 'id', VARIADIC ARRAY[]::text[]);");
            migrationBuilder.Sql("SELECT audit.attach_table_audit('app', 'habit_checks', 'id', VARIADIC ARRAY[]::text[]);");

            // Permisos mínimos para el rol de la aplicación (convención app_user).
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON app.habit_templates TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON app.habit_checks TO app_user;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS habit_templates_audit ON app.habit_templates;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS habit_checks_audit ON app.habit_checks;");

            migrationBuilder.DropTable(
                name: "habit_checks",
                schema: "app");

            migrationBuilder.DropTable(
                name: "habit_templates",
                schema: "app");
        }
    }
}
