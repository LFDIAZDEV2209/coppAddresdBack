using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Community.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityPolls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "polls",
                schema: "community",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polls", x => x.id);
                    table.ForeignKey(
                        name: "FK_polls_posts_post_id",
                        column: x => x.post_id,
                        principalSchema: "community",
                        principalTable: "posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "poll_options",
                schema: "community",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    poll_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_poll_options", x => x.id);
                    table.ForeignKey(
                        name: "FK_poll_options_polls_poll_id",
                        column: x => x.poll_id,
                        principalSchema: "community",
                        principalTable: "polls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "poll_votes",
                schema: "community",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    option_id = table.Column<Guid>(type: "uuid", nullable: false),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_poll_votes", x => x.id);
                    table.ForeignKey(
                        name: "FK_poll_votes_poll_options_option_id",
                        column: x => x.option_id,
                        principalSchema: "community",
                        principalTable: "poll_options",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_poll_options_poll_position",
                schema: "community",
                table: "poll_options",
                columns: new[] { "poll_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_poll_votes_profile_id",
                schema: "community",
                table: "poll_votes",
                column: "profile_id");

            migrationBuilder.CreateIndex(
                name: "ux_poll_votes_option_profile",
                schema: "community",
                table: "poll_votes",
                columns: new[] { "option_id", "profile_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_polls_post_id",
                schema: "community",
                table: "polls",
                column: "post_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "poll_votes",
                schema: "community");

            migrationBuilder.DropTable(
                name: "poll_options",
                schema: "community");

            migrationBuilder.DropTable(
                name: "polls",
                schema: "community");
        }
    }
}
