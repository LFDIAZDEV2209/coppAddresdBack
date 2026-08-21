using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Community.Migrations
{
    /// <inheritdoc />
    public partial class AddFollowsAndMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "follows",
                schema: "community",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    follower_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    following_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_follows", x => x.id);
                    table.ForeignKey(
                        name: "FK_follows_profiles_follower_profile_id",
                        column: x => x.follower_profile_id,
                        principalSchema: "community",
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_follows_profiles_following_profile_id",
                        column: x => x.following_profile_id,
                        principalSchema: "community",
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                schema: "community",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    sender_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_messages_profiles_recipient_profile_id",
                        column: x => x.recipient_profile_id,
                        principalSchema: "community",
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_messages_profiles_sender_profile_id",
                        column: x => x.sender_profile_id,
                        principalSchema: "community",
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_follows_follower_following",
                schema: "community",
                table: "follows",
                columns: new[] { "follower_profile_id", "following_profile_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_follows_following",
                schema: "community",
                table: "follows",
                column: "following_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_messages_recipient_profile_id",
                schema: "community",
                table: "messages",
                column: "recipient_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_messages_sender_recipient_created",
                schema: "community",
                table: "messages",
                columns: new[] { "sender_profile_id", "recipient_profile_id", "created_at" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "follows",
                schema: "community");

            migrationBuilder.DropTable(
                name: "messages",
                schema: "community");
        }
    }
}
