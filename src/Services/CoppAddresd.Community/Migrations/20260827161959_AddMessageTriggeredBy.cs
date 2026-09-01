using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Community.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageTriggeredBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "triggered_by_profile_id",
                schema: "community",
                table: "messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_messages_triggered_by_profile_id",
                schema: "community",
                table: "messages",
                column: "triggered_by_profile_id");

            migrationBuilder.AddForeignKey(
                name: "FK_messages_profiles_triggered_by_profile_id",
                schema: "community",
                table: "messages",
                column: "triggered_by_profile_id",
                principalSchema: "community",
                principalTable: "profiles",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_messages_profiles_triggered_by_profile_id",
                schema: "community",
                table: "messages");

            migrationBuilder.DropIndex(
                name: "IX_messages_triggered_by_profile_id",
                schema: "community",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "triggered_by_profile_id",
                schema: "community",
                table: "messages");
        }
    }
}
