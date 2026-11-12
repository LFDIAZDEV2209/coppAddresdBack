using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.WhatsApp.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PatientLinksAndConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "conversations",
                schema: "whatsapp",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    thread_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    language = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    last_inbound_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "patient_links",
                schema: "whatsapp",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    wa_id = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    first_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    language = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    opt_out = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patient_links", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_conversations_patient_id",
                schema: "whatsapp",
                table: "conversations",
                column: "patient_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_patient_links_patient_id",
                schema: "whatsapp",
                table: "patient_links",
                column: "patient_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_patient_links_wa_id",
                schema: "whatsapp",
                table: "patient_links",
                column: "wa_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "conversations",
                schema: "whatsapp");

            migrationBuilder.DropTable(
                name: "patient_links",
                schema: "whatsapp");
        }
    }
}
