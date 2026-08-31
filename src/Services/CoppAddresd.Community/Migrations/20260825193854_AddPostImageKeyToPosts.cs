using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Community.Migrations
{
    /// <inheritdoc />
    public partial class AddPostImageKeyToPosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "image_key",
                schema: "community",
                table: "posts",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "image_key",
                schema: "community",
                table: "posts");
        }
    }
}
