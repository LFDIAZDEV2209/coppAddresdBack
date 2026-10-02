using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSosDeliveryStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "sms_delivery_status",
                schema: "app",
                table: "sos_alerts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "voice_answered_by",
                schema: "app",
                table: "sos_alerts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "voice_call_status",
                schema: "app",
                table: "sos_alerts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "voice_duration_seconds",
                schema: "app",
                table: "sos_alerts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "voice_provider_call_id",
                schema: "app",
                table: "sos_alerts",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "sms_delivery_status",
                schema: "app",
                table: "sos_alerts");

            migrationBuilder.DropColumn(
                name: "voice_answered_by",
                schema: "app",
                table: "sos_alerts");

            migrationBuilder.DropColumn(
                name: "voice_call_status",
                schema: "app",
                table: "sos_alerts");

            migrationBuilder.DropColumn(
                name: "voice_duration_seconds",
                schema: "app",
                table: "sos_alerts");

            migrationBuilder.DropColumn(
                name: "voice_provider_call_id",
                schema: "app",
                table: "sos_alerts");
        }
    }
}
