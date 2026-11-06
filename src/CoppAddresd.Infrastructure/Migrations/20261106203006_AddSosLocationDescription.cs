using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSosLocationDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "location_description",
                schema: "app",
                table: "sos_alerts",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "location_lookup_completed_at",
                schema: "app",
                table: "sos_alerts",
                type: "timestamptz",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "location_description",
                schema: "app",
                table: "sos_alerts");

            migrationBuilder.DropColumn(
                name: "location_lookup_completed_at",
                schema: "app",
                table: "sos_alerts");
        }
    }
}
