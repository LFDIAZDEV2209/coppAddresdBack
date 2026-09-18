using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Telemedicine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTelemedicineNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Defaults de F2 fijados a nivel BD (no false/0) para que las filas de
            // settings ya existentes queden con recordatorios ACTIVOS: notificaciones
            // habilitadas y ventanas 24 h / 1 h / SMS 1 h (los defaults del dominio).
            migrationBuilder.AddColumn<bool>(
                name: "notifications_enabled",
                schema: "tele",
                table: "telemedicine_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "reminder_first_hours_before",
                schema: "tele",
                table: "telemedicine_settings",
                type: "integer",
                nullable: false,
                defaultValue: 24);

            migrationBuilder.AddColumn<int>(
                name: "reminder_second_hours_before",
                schema: "tele",
                table: "telemedicine_settings",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "sms_reminder_hours_before",
                schema: "tele",
                table: "telemedicine_settings",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "notification_dispatch",
                schema: "tele",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_dispatch", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notification_dispatch_appointment_id_kind",
                schema: "tele",
                table: "notification_dispatch",
                columns: new[] { "appointment_id", "kind" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_dispatch",
                schema: "tele");

            migrationBuilder.DropColumn(
                name: "notifications_enabled",
                schema: "tele",
                table: "telemedicine_settings");

            migrationBuilder.DropColumn(
                name: "reminder_first_hours_before",
                schema: "tele",
                table: "telemedicine_settings");

            migrationBuilder.DropColumn(
                name: "reminder_second_hours_before",
                schema: "tele",
                table: "telemedicine_settings");

            migrationBuilder.DropColumn(
                name: "sms_reminder_hours_before",
                schema: "tele",
                table: "telemedicine_settings");
        }
    }
}
