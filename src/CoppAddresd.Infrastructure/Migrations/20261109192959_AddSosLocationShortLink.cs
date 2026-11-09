using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSosLocationShortLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "location_link_hash",
                schema: "app",
                table: "sos_alerts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            // La tabla recibe alertas mientras se despliega; el índice no bloquea escrituras.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX CONCURRENTLY ux_sos_alerts_location_link_hash " +
                "ON app.sos_alerts (location_link_hash) WHERE location_link_hash IS NOT NULL;",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY app.ux_sos_alerts_location_link_hash;",
                suppressTransaction: true);

            migrationBuilder.DropColumn(
                name: "location_link_hash",
                schema: "app",
                table: "sos_alerts");
        }
    }
}
