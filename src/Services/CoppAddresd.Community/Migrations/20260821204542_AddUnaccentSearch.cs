using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Community.Migrations
{
    /// <inheritdoc />
    public partial class AddUnaccentSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
