using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSosVoiceChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "voice_channel_status",
                schema: "app",
                table: "sos_alerts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "pendiente");

            migrationBuilder.AddColumn<string>(
                name: "voice_detail",
                schema: "app",
                table: "sos_alerts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "voice_updated_at",
                schema: "app",
                table: "sos_alerts",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "voice_status",
                schema: "app",
                table: "notification_dedupe_keys",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "voice_channel_status",
                schema: "app",
                table: "sos_alerts");

            migrationBuilder.DropColumn(
                name: "voice_detail",
                schema: "app",
                table: "sos_alerts");

            migrationBuilder.DropColumn(
                name: "voice_updated_at",
                schema: "app",
                table: "sos_alerts");

            migrationBuilder.DropColumn(
                name: "voice_status",
                schema: "app",
                table: "notification_dedupe_keys");
        }
    }
}
