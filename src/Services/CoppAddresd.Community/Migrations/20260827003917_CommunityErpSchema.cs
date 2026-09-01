using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Community.Migrations
{
    /// <inheritdoc />
    public partial class CommunityErpSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "best_streak",
                schema: "community",
                table: "profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "current_streak",
                schema: "community",
                table: "profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "diagnosis",
                schema: "community",
                table: "profiles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_active_at",
                schema: "community",
                table: "profiles",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_post_at",
                schema: "community",
                table: "profiles",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "region",
                schema: "community",
                table: "profiles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "week",
                schema: "community",
                table: "profiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "xp_total",
                schema: "community",
                table: "profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "destination",
                schema: "community",
                table: "posts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "type",
                schema: "community",
                table: "posts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "view_count",
                schema: "community",
                table: "posts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "feed_events",
                schema: "community",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    body = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feed_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_feed_events_profiles_profile_id",
                        column: x => x.profile_id,
                        principalSchema: "community",
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "xp_entries",
                schema: "community",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xp_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_xp_entries_profiles_profile_id",
                        column: x => x.profile_id,
                        principalSchema: "community",
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_profiles_region",
                schema: "community",
                table: "profiles",
                column: "region");

            migrationBuilder.CreateIndex(
                name: "ix_feed_events_created_at",
                schema: "community",
                table: "feed_events",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_feed_events_profile_id",
                schema: "community",
                table: "feed_events",
                column: "profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_xp_entries_created_at",
                schema: "community",
                table: "xp_entries",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_xp_entries_profile_id",
                schema: "community",
                table: "xp_entries",
                column: "profile_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "feed_events",
                schema: "community");

            migrationBuilder.DropTable(
                name: "xp_entries",
                schema: "community");

            migrationBuilder.DropIndex(
                name: "ix_profiles_region",
                schema: "community",
                table: "profiles");

            migrationBuilder.DropColumn(
                name: "best_streak",
                schema: "community",
                table: "profiles");

            migrationBuilder.DropColumn(
                name: "current_streak",
                schema: "community",
                table: "profiles");

            migrationBuilder.DropColumn(
                name: "diagnosis",
                schema: "community",
                table: "profiles");

            migrationBuilder.DropColumn(
                name: "last_active_at",
                schema: "community",
                table: "profiles");

            migrationBuilder.DropColumn(
                name: "last_post_at",
                schema: "community",
                table: "profiles");

            migrationBuilder.DropColumn(
                name: "region",
                schema: "community",
                table: "profiles");

            migrationBuilder.DropColumn(
                name: "week",
                schema: "community",
                table: "profiles");

            migrationBuilder.DropColumn(
                name: "xp_total",
                schema: "community",
                table: "profiles");

            migrationBuilder.DropColumn(
                name: "destination",
                schema: "community",
                table: "posts");

            migrationBuilder.DropColumn(
                name: "type",
                schema: "community",
                table: "posts");

            migrationBuilder.DropColumn(
                name: "view_count",
                schema: "community",
                table: "posts");
        }
    }
}
