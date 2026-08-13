using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Auth.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationsAndUserApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ApplicationId",
                schema: "auth",
                table: "RefreshTokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Applications",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserApplications",
                schema: "auth",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserApplications", x => new { x.UserId, x.ApplicationId });
                    table.ForeignKey(
                        name: "FK_UserApplications_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "auth",
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserApplications_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_ApplicationId",
                schema: "auth",
                table: "RefreshTokens",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Code",
                schema: "auth",
                table: "Applications",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserApplications_ApplicationId",
                schema: "auth",
                table: "UserApplications",
                column: "ApplicationId");

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_Applications_ApplicationId",
                schema: "auth",
                table: "RefreshTokens",
                column: "ApplicationId",
                principalSchema: "auth",
                principalTable: "Applications",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_Applications_ApplicationId",
                schema: "auth",
                table: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "UserApplications",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "Applications",
                schema: "auth");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_ApplicationId",
                schema: "auth",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "ApplicationId",
                schema: "auth",
                table: "RefreshTokens");
        }
    }
}
