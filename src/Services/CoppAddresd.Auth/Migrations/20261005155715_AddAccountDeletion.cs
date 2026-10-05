using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Auth.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletionRequestedAt",
                schema: "auth",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PurgeAfter",
                schema: "auth",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PurgedAt",
                schema: "auth",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_PurgeAfter",
                schema: "auth",
                table: "Users",
                column: "PurgeAfter",
                filter: "\"PurgeAfter\" IS NOT NULL AND \"PurgedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_PurgeAfter",
                schema: "auth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletionRequestedAt",
                schema: "auth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PurgeAfter",
                schema: "auth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PurgedAt",
                schema: "auth",
                table: "Users");
        }
    }
}
