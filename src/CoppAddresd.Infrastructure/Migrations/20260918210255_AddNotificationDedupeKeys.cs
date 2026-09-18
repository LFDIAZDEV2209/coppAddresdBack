using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationDedupeKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_dedupe_keys",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    dedupe_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    push_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    sms_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_dedupe_keys", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "uq_notification_dedupe_keys_dedupe_key",
                schema: "app",
                table: "notification_dedupe_keys",
                column: "dedupe_key",
                unique: true);

            // Dedupe operativo (F2): clave opaca + usuario + estados por canal,
            // sin PHI → sin trigger de auditoría. Permisos mínimos para app_user
            // (convención del schema app).
            migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON app.notification_dedupe_keys TO app_user;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_dedupe_keys",
                schema: "app");
        }
    }
}
