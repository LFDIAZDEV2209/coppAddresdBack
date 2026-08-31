using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Community.Migrations
{
    /// <inheritdoc />
    public partial class AddReposts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reposts",
                schema: "community",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reposts", x => x.id);
                    table.ForeignKey(
                        name: "FK_reposts_posts_post_id",
                        column: x => x.post_id,
                        principalSchema: "community",
                        principalTable: "posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_reposts_profiles_profile_id",
                        column: x => x.profile_id,
                        principalSchema: "community",
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_reposts_post_id",
                schema: "community",
                table: "reposts",
                column: "post_id");

            migrationBuilder.CreateIndex(
                name: "ix_reposts_post_profile",
                schema: "community",
                table: "reposts",
                columns: new[] { "post_id", "profile_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reposts_profile_id",
                schema: "community",
                table: "reposts",
                column: "profile_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reposts",
                schema: "community");
        }
    }
}
