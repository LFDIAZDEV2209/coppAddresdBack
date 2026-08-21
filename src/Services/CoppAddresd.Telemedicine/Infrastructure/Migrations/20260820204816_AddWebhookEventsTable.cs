using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Telemedicine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookEventsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "telemedicine_webhook_events",
                schema: "tele",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    room_sid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    participant_sid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    payload_json = table.Column<string>(type: "text", nullable: true),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_telemedicine_webhook_events", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_telemedicine_webhook_events_event_type_room_sid_participant",
                schema: "tele",
                table: "telemedicine_webhook_events",
                columns: new[] { "event_type", "room_sid", "participant_sid" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "telemedicine_webhook_events",
                schema: "tele");
        }
    }
}
