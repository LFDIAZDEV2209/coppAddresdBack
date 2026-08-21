using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Community.Migrations
{
    /// <inheritdoc />
    public partial class InitialCommunitySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "community");

            migrationBuilder.CreateTable(
                name: "profiles",
                schema: "community",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    bio = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    avatar_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "posts",
                schema: "community",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    pinned = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_posts", x => x.id);
                    table.ForeignKey(
                        name: "FK_posts_profiles_profile_id",
                        column: x => x.profile_id,
                        principalSchema: "community",
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "comments",
                schema: "community",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_comment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    deleted_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comments", x => x.id);
                    table.ForeignKey(
                        name: "FK_comments_comments_parent_comment_id",
                        column: x => x.parent_comment_id,
                        principalSchema: "community",
                        principalTable: "comments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_comments_posts_post_id",
                        column: x => x.post_id,
                        principalSchema: "community",
                        principalTable: "posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_comments_profiles_profile_id",
                        column: x => x.profile_id,
                        principalSchema: "community",
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "likes",
                schema: "community",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    post_id = table.Column<Guid>(type: "uuid", nullable: true),
                    comment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_likes", x => x.id);
                    table.ForeignKey(
                        name: "FK_likes_comments_comment_id",
                        column: x => x.comment_id,
                        principalSchema: "community",
                        principalTable: "comments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_likes_posts_post_id",
                        column: x => x.post_id,
                        principalSchema: "community",
                        principalTable: "posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_likes_profiles_profile_id",
                        column: x => x.profile_id,
                        principalSchema: "community",
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_comments_parent_comment_id",
                schema: "community",
                table: "comments",
                column: "parent_comment_id");

            migrationBuilder.CreateIndex(
                name: "ix_comments_post_id",
                schema: "community",
                table: "comments",
                column: "post_id");

            migrationBuilder.CreateIndex(
                name: "IX_comments_profile_id",
                schema: "community",
                table: "comments",
                column: "profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_likes_comment_id",
                schema: "community",
                table: "likes",
                column: "comment_id");

            migrationBuilder.CreateIndex(
                name: "ix_likes_comment_profile",
                schema: "community",
                table: "likes",
                columns: new[] { "comment_id", "profile_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_likes_post_id",
                schema: "community",
                table: "likes",
                column: "post_id");

            migrationBuilder.CreateIndex(
                name: "ix_likes_post_profile",
                schema: "community",
                table: "likes",
                columns: new[] { "post_id", "profile_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_likes_profile_id",
                schema: "community",
                table: "likes",
                column: "profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_posts_created_at",
                schema: "community",
                table: "posts",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_posts_profile_id",
                schema: "community",
                table: "posts",
                column: "profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_profiles_status",
                schema: "community",
                table: "profiles",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_profiles_user_id",
                schema: "community",
                table: "profiles",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "likes",
                schema: "community");

            migrationBuilder.DropTable(
                name: "comments",
                schema: "community");

            migrationBuilder.DropTable(
                name: "posts",
                schema: "community");

            migrationBuilder.DropTable(
                name: "profiles",
                schema: "community");
        }
    }
}
