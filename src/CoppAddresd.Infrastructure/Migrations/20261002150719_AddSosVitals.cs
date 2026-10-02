using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSosVitals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "blood_pressure",
                schema: "app",
                table: "sos_alerts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "heart_rate",
                schema: "app",
                table: "sos_alerts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "spo2",
                schema: "app",
                table: "sos_alerts",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "blood_pressure",
                schema: "app",
                table: "sos_alerts");

            migrationBuilder.DropColumn(
                name: "heart_rate",
                schema: "app",
                table: "sos_alerts");

            migrationBuilder.DropColumn(
                name: "spo2",
                schema: "app",
                table: "sos_alerts");
        }
    }
}
