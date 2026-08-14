using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaItemContentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "author",
                schema: "app",
                table: "media_items",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "category",
                schema: "app",
                table: "media_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "day",
                schema: "app",
                table: "media_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "month",
                schema: "app",
                table: "media_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "thumbnail_key",
                schema: "app",
                table: "media_items",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_media_items_month_day",
                schema: "app",
                table: "media_items",
                columns: new[] { "month", "day" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_media_items_month_day",
                schema: "app",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "author",
                schema: "app",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "category",
                schema: "app",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "day",
                schema: "app",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "month",
                schema: "app",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "thumbnail_key",
                schema: "app",
                table: "media_items");
        }
    }
}
