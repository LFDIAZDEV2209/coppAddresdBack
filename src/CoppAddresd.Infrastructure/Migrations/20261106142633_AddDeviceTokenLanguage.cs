using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceTokenLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "language",
                schema: "app",
                table: "device_tokens",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "es");

            migrationBuilder.AddCheckConstraint(
                name: "ck_device_tokens_language",
                schema: "app",
                table: "device_tokens",
                sql: "language IN ('es', 'en')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_device_tokens_language",
                schema: "app",
                table: "device_tokens");

            migrationBuilder.DropColumn(
                name: "language",
                schema: "app",
                table: "device_tokens");
        }
    }
}
