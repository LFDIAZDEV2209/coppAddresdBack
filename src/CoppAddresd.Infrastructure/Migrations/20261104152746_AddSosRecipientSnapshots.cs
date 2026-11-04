using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSosRecipientSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sos_alert_recipients",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    alert_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contact_order = table.Column<int>(type: "integer", nullable: false),
                    phone_e164 = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    sms_channel_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sms_detail = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    sms_provider_message_id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    sms_delivery_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    sms_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    voice_channel_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    voice_detail = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    voice_provider_call_id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    voice_call_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    voice_answered_by = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    voice_duration_seconds = table.Column<int>(type: "integer", nullable: true),
                    voice_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    email_channel_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sos_alert_recipients", x => x.id);
                    table.ForeignKey(
                        name: "FK_sos_alert_recipients_sos_alerts_alert_id",
                        column: x => x.alert_id,
                        principalSchema: "app",
                        principalTable: "sos_alerts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "uq_sos_recipient_contact",
                schema: "app",
                table: "sos_alert_recipients",
                columns: new[] { "alert_id", "contact_order" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sos_alert_recipients",
                schema: "app");
        }
    }
}
