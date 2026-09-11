using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramMilestoneSends : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "program_milestone_sends",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    milestone_day = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    thread_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    sent_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_milestone_sends", x => x.id);
                    table.ForeignKey(
                        name: "FK_program_milestone_sends_program_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalSchema: "app",
                        principalTable: "program_enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_program_milestone_sends_enrollment_day",
                schema: "app",
                table: "program_milestone_sends",
                columns: new[] { "enrollment_id", "milestone_day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_program_milestone_sends_status",
                schema: "app",
                table: "program_milestone_sends",
                column: "status");

            // Auditoría trigger-based (SPEC §8.5): la tabla no contiene PHI (solo
            // ids de inscripción, día de hito y estado del envío), por lo que su
            // DML queda auditado como el resto del módulo (precedente:
            // notifications / streak_states).
            migrationBuilder.Sql("SELECT audit.attach_table_audit('app', 'program_milestone_sends', 'id', VARIADIC ARRAY[]::text[]);");

            // Permisos mínimos para el rol de la aplicación (convención app_user).
            migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON app.program_milestone_sends TO app_user;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS program_milestone_sends_audit ON app.program_milestone_sends;");

            migrationBuilder.DropTable(
                name: "program_milestone_sends",
                schema: "app");
        }
    }
}
