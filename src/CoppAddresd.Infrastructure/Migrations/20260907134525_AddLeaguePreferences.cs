using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaguePreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "league_nickname",
                schema: "app",
                table: "patient_profiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "league_opt_in",
                schema: "app",
                table: "patient_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_patient_profiles_league_opt_in",
                schema: "app",
                table: "patient_profiles",
                column: "league_opt_in",
                filter: "\"league_opt_in\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_patient_profiles_league_opt_in",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "league_nickname",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "league_opt_in",
                schema: "app",
                table: "patient_profiles");
        }
    }
}
