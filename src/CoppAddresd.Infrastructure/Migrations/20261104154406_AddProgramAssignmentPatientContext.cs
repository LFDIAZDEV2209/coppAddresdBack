using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramAssignmentPatientContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "timezone",
                schema: "app",
                table: "patient_profiles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "America/Bogota");

            migrationBuilder.AddColumn<string>(
                name: "client_request_id",
                schema: "app",
                table: "health_test_evaluations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "timezone",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "client_request_id",
                schema: "app",
                table: "health_test_evaluations");
        }
    }
}
