using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Telemedicine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentReopenTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "completed_at",
                schema: "tele",
                table: "appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "reopen_count",
                schema: "tele",
                table: "appointments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reopened_at",
                schema: "tele",
                table: "appointments",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "completed_at",
                schema: "tele",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "reopen_count",
                schema: "tele",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "reopened_at",
                schema: "tele",
                table: "appointments");
        }
    }
}
