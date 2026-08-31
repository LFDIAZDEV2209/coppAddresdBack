using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramProgressNbStreak : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "nb_current_streak",
                schema: "app",
                table: "streak_states",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "nb_last_completed_date",
                schema: "app",
                table: "streak_states",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "nb_longest_streak",
                schema: "app",
                table: "streak_states",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "nb_current_streak",
                schema: "app",
                table: "streak_states");

            migrationBuilder.DropColumn(
                name: "nb_last_completed_date",
                schema: "app",
                table: "streak_states");

            migrationBuilder.DropColumn(
                name: "nb_longest_streak",
                schema: "app",
                table: "streak_states");
        }
    }
}
