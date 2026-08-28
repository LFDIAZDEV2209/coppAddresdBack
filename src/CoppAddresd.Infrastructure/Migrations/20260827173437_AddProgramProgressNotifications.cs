using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramProgressNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notifications",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "normal"),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "push"),
                    sent_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    read_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_notifications_patient_profiles_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_patient_sent_at",
                schema: "app",
                table: "notifications",
                columns: new[] { "patient_id", "sent_at" },
                descending: new[] { false, true });

            // Auditoría trigger-based (SPEC §8.5): la tabla no contiene PHI
            // (copy de gamificación + timestamps; a diferencia de
            // emotional_records no hay mood/notas que excluir), por lo que su
            // DML queda auditado como el resto del módulo (precedente:
            // habit_checks / clinical_xp_reviews).
            migrationBuilder.Sql("SELECT audit.attach_table_audit('app', 'notifications', 'id', VARIADIC ARRAY[]::text[]);");

            // Permisos mínimos para el rol de la aplicación (convención app_user).
            migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON app.notifications TO app_user;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS notifications_audit ON app.notifications;");

            migrationBuilder.DropTable(
                name: "notifications",
                schema: "app");
        }
    }
}
