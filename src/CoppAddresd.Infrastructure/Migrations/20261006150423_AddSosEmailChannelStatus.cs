using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSosEmailChannelStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "destination_email",
                schema: "app",
                table: "sos_alerts",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email_channel_status",
                schema: "app",
                table: "sos_alerts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pendiente");

            migrationBuilder.AddColumn<DateTime>(
                name: "email_updated_at",
                schema: "app",
                table: "sos_alerts",
                type: "timestamptz",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "destination_email",
                schema: "app",
                table: "sos_alerts");

            migrationBuilder.DropColumn(
                name: "email_channel_status",
                schema: "app",
                table: "sos_alerts");

            migrationBuilder.DropColumn(
                name: "email_updated_at",
                schema: "app",
                table: "sos_alerts");
        }
    }
}
