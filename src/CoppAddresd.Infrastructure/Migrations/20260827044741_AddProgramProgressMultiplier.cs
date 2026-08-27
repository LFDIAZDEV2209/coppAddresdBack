using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramProgressMultiplier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "multiplier_used",
                schema: "app",
                table: "xp_ledger",
                type: "numeric(4,2)",
                precision: 4,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "multiplier_active",
                schema: "app",
                table: "streak_states",
                type: "numeric(4,2)",
                precision: 4,
                scale: 2,
                nullable: false,
                defaultValue: 1.0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "multiplier_ends_at",
                schema: "app",
                table: "streak_states",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_streak_states_multiplier_active_positive",
                schema: "app",
                table: "streak_states",
                sql: "\"multiplier_active\" >= 1.0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_streak_states_multiplier_active_positive",
                schema: "app",
                table: "streak_states");

            migrationBuilder.DropColumn(
                name: "multiplier_used",
                schema: "app",
                table: "xp_ledger");

            migrationBuilder.DropColumn(
                name: "multiplier_active",
                schema: "app",
                table: "streak_states");

            migrationBuilder.DropColumn(
                name: "multiplier_ends_at",
                schema: "app",
                table: "streak_states");
        }
    }
}
