using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.WhatsApp.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProgramAccompanimentReasons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "detail",
                schema: "whatsapp",
                table: "reminders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "detail",
                schema: "whatsapp",
                table: "reminders");
        }
    }
}
