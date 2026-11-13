using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSosWhatsAppOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sos_whatsapp_deliveries",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    alert_id = table.Column<Guid>(type: "uuid", nullable: false),
                    destination = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    patient_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    contact_order = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    parameters_json = table.Column<string>(type: "jsonb", nullable: true),
                    wamid = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    error_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    lease_token = table.Column<Guid>(type: "uuid", nullable: true),
                    lease_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    delivery_rank = table.Column<int>(type: "integer", nullable: false),
                    delivery_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sos_whatsapp_deliveries", x => x.id);
                    table.ForeignKey(
                        name: "FK_sos_whatsapp_deliveries_sos_alerts_alert_id",
                        column: x => x.alert_id,
                        principalSchema: "app",
                        principalTable: "sos_alerts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sos_whatsapp_deliveries_alert_id_destination",
                schema: "app",
                table: "sos_whatsapp_deliveries",
                columns: new[] { "alert_id", "destination" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sos_whatsapp_deliveries_lease_until",
                schema: "app",
                table: "sos_whatsapp_deliveries",
                column: "lease_until",
                filter: "state = 'Processing'");

            migrationBuilder.CreateIndex(
                name: "IX_sos_whatsapp_deliveries_state_next_attempt_at",
                schema: "app",
                table: "sos_whatsapp_deliveries",
                columns: new[] { "state", "next_attempt_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sos_whatsapp_deliveries",
                schema: "app");
        }
    }
}
