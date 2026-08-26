using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Community.Migrations
{
    /// <inheritdoc />
    public partial class ChangeProfileStatusToActiveBanned : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "reviewed_by",
                schema: "community",
                table: "profiles",
                newName: "banned_by");

            migrationBuilder.RenameColumn(
                name: "reviewed_at",
                schema: "community",
                table: "profiles",
                newName: "banned_at");

            migrationBuilder.RenameColumn(
                name: "rejection_reason",
                schema: "community",
                table: "profiles",
                newName: "ban_reason");

            // El valor del enum cambió pero la columna (text) no, así que migramos los datos existentes.
            migrationBuilder.Sql("UPDATE community.profiles SET status = 'Active' WHERE status IN ('Pending', 'Approved');");
            migrationBuilder.Sql("UPDATE community.profiles SET status = 'Banned' WHERE status = 'Rejected';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "banned_by",
                schema: "community",
                table: "profiles",
                newName: "reviewed_by");

            migrationBuilder.RenameColumn(
                name: "banned_at",
                schema: "community",
                table: "profiles",
                newName: "reviewed_at");

            migrationBuilder.RenameColumn(
                name: "ban_reason",
                schema: "community",
                table: "profiles",
                newName: "rejection_reason");

            // Reversión de los UPDATEs de arriba (mapeo de vuelta a los valores previos).
            migrationBuilder.Sql("UPDATE community.profiles SET status = 'Pending' WHERE status = 'Active';");
            migrationBuilder.Sql("UPDATE community.profiles SET status = 'Rejected' WHERE status = 'Banned';");
        }
    }
}
