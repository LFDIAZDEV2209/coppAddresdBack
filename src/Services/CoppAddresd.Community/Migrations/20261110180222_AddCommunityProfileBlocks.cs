using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Community.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityProfileBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "profile_blocks",
                schema: "community",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    blocker_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    blocked_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_blocks", x => x.id);
                    table.CheckConstraint("ck_profile_blocks_not_self", "blocker_profile_id <> blocked_profile_id");
                    table.ForeignKey(
                        name: "FK_profile_blocks_profiles_blocked_profile_id",
                        column: x => x.blocked_profile_id,
                        principalSchema: "community",
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_profile_blocks_profiles_blocker_profile_id",
                        column: x => x.blocker_profile_id,
                        principalSchema: "community",
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_profile_blocks_blocked",
                schema: "community",
                table: "profile_blocks",
                column: "blocked_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_profile_blocks_blocker_blocked",
                schema: "community",
                table: "profile_blocks",
                columns: new[] { "blocker_profile_id", "blocked_profile_id" },
                unique: true);


            // Verify every ancestor, including legacy nested replies. Cycles,
            // missing ancestors and paths over 100 levels fail closed.
            migrationBuilder.Sql("""
                CREATE FUNCTION community.is_comment_thread_visible(viewer uuid, root_comment uuid)
                RETURNS boolean
                LANGUAGE sql STABLE
                AS $$
                  WITH RECURSIVE ancestors AS (
                    SELECT c.id, c.profile_id, c.post_id, c.parent_comment_id, c.deleted_at,
                           ARRAY[c.id] AS path, false AS cycle, 1 AS depth
                    FROM community.comments c WHERE c.id = root_comment
                    UNION ALL
                    SELECT c.id, c.profile_id, c.post_id, c.parent_comment_id, c.deleted_at,
                           a.path || c.id, c.id = ANY(a.path), a.depth + 1
                    FROM ancestors a
                    JOIN community.comments c ON c.id = a.parent_comment_id
                    WHERE NOT a.cycle AND a.depth < 100
                  )
                  SELECT EXISTS (SELECT 1 FROM ancestors)
                     AND NOT EXISTS (
                       SELECT 1 FROM ancestors a
                       LEFT JOIN community.posts p ON p.id = a.post_id
                       WHERE a.deleted_at IS NOT NULL OR p.id IS NULL OR p.deleted_at IS NOT NULL
                          OR a.cycle OR (a.depth = 100 AND a.parent_comment_id IS NOT NULL)
                          OR (a.parent_comment_id IS NOT NULL AND NOT EXISTS (
                              SELECT 1 FROM community.comments parent WHERE parent.id = a.parent_comment_id))
                          OR EXISTS (
                              SELECT 1 FROM community.profile_blocks b
                              WHERE (b.blocker_profile_id = viewer
                                       AND b.blocked_profile_id IN (a.profile_id, p.profile_id))
                                 OR (b.blocked_profile_id = viewer
                                       AND b.blocker_profile_id IN (a.profile_id, p.profile_id)))
                     );
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION community.is_comment_thread_visible(uuid, uuid);");
            migrationBuilder.DropTable(
                name: "profile_blocks",
                schema: "community");
        }
    }
}
