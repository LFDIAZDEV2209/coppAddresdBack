using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.WhatsApp.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WhatsAppOtpBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "binding_sessions",
                schema: "whatsapp",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wa_id = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: true),
                    contact_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    state = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    otp_sends = table.Column<int>(type: "integer", nullable: false),
                    otp_sent_at = table.Column<DateTimeOffset[]>(type: "timestamp with time zone[]", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    lease_owner = table.Column<Guid>(type: "uuid", nullable: true),
                    lease_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_event_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_reply = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_binding_sessions", x => x.id);
                    table.CheckConstraint("ck_binding_limits", "attempts BETWEEN 0 AND 3 AND otp_sends BETWEEN 0 AND 2 AND cardinality(otp_sent_at) <= 2");
                });

            migrationBuilder.CreateIndex(
                name: "IX_binding_sessions_wa_id",
                schema: "whatsapp",
                table: "binding_sessions",
                column: "wa_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "binding_sessions",
                schema: "whatsapp");
        }
    }
}
