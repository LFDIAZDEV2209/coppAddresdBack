using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Community.Migrations
{
    /// <inheritdoc />
    public partial class AddClubs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "club_id",
                schema: "community",
                table: "posts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "club_status",
                schema: "community",
                table: "posts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "club_visibility",
                schema: "community",
                table: "posts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "featured",
                schema: "community",
                table: "posts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "scheduled_for",
                schema: "community",
                table: "posts",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "club_categories",
                schema: "community",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    icon = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_club_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "clubs",
                schema: "community",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    rules = table.Column<string[]>(type: "text[]", nullable: false),
                    objectives = table.Column<string[]>(type: "text[]", nullable: false),
                    category = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    tags = table.Column<string[]>(type: "text[]", nullable: false),
                    cover_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    logo_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    visibility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    max_members = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_by_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clubs", x => x.id);
                    table.ForeignKey(
                        name: "FK_clubs_profiles_created_by_profile_id",
                        column: x => x.created_by_profile_id,
                        principalSchema: "community",
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "club_events",
                schema: "community",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    club_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    ends_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    location = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    meeting_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    max_attendees = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    confirmed_count = table.Column<int>(type: "integer", nullable: false),
                    waitlist_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_club_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_club_events_clubs_club_id",
                        column: x => x.club_id,
                        principalSchema: "community",
                        principalTable: "clubs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "club_invitations",
                schema: "community",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    club_id = table.Column<Guid>(type: "uuid", nullable: false),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_by_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_club_invitations", x => x.id);
                    table.ForeignKey(
                        name: "FK_club_invitations_clubs_club_id",
                        column: x => x.club_id,
                        principalSchema: "community",
                        principalTable: "clubs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "club_members",
                schema: "community",
                columns: table => new
                {
                    club_id = table.Column<Guid>(type: "uuid", nullable: false),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    muted_until = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    joined_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_club_members", x => new { x.club_id, x.profile_id });
                    table.ForeignKey(
                        name: "FK_club_members_clubs_club_id",
                        column: x => x.club_id,
                        principalSchema: "community",
                        principalTable: "clubs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_club_members_profiles_profile_id",
                        column: x => x.profile_id,
                        principalSchema: "community",
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "club_notifications",
                schema: "community",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    club_id = table.Column<Guid>(type: "uuid", nullable: false),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    payload = table.Column<string>(type: "text", nullable: true),
                    read_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_club_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_club_notifications_clubs_club_id",
                        column: x => x.club_id,
                        principalSchema: "community",
                        principalTable: "clubs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_club_notifications_profiles_profile_id",
                        column: x => x.profile_id,
                        principalSchema: "community",
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "moderation_logs",
                schema: "community",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    club_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_moderation_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_moderation_logs_clubs_club_id",
                        column: x => x.club_id,
                        principalSchema: "community",
                        principalTable: "clubs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_moderation_logs_profiles_actor_profile_id",
                        column: x => x.actor_profile_id,
                        principalSchema: "community",
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_moderation_logs_profiles_target_profile_id",
                        column: x => x.target_profile_id,
                        principalSchema: "community",
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "event_attendances",
                schema: "community",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_attendances", x => x.id);
                    table.ForeignKey(
                        name: "FK_event_attendances_club_events_event_id",
                        column: x => x.event_id,
                        principalSchema: "community",
                        principalTable: "club_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_attendances_profiles_profile_id",
                        column: x => x.profile_id,
                        principalSchema: "community",
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "live_sessions",
                schema: "community",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    club_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    scheduled_start_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    embed_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    provider_room = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_live_sessions_club_events_event_id",
                        column: x => x.event_id,
                        principalSchema: "community",
                        principalTable: "club_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_live_sessions_clubs_club_id",
                        column: x => x.club_id,
                        principalSchema: "community",
                        principalTable: "clubs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "live_chat_messages",
                schema: "community",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    live_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    sent_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_chat_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_live_chat_messages_live_sessions_live_session_id",
                        column: x => x.live_session_id,
                        principalSchema: "community",
                        principalTable: "live_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_live_chat_messages_profiles_sender_profile_id",
                        column: x => x.sender_profile_id,
                        principalSchema: "community",
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_posts_club_id_club_status",
                schema: "community",
                table: "posts",
                columns: new[] { "club_id", "club_status" });

            migrationBuilder.CreateIndex(
                name: "ix_posts_club_id_created_at",
                schema: "community",
                table: "posts",
                columns: new[] { "club_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_club_categories_slug",
                schema: "community",
                table: "club_categories",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_club_events_club_id_starts_at",
                schema: "community",
                table: "club_events",
                columns: new[] { "club_id", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_club_invitations_club_id",
                schema: "community",
                table: "club_invitations",
                column: "club_id");

            migrationBuilder.CreateIndex(
                name: "ix_club_invitations_token",
                schema: "community",
                table: "club_invitations",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_club_members_club_id_role",
                schema: "community",
                table: "club_members",
                columns: new[] { "club_id", "role" });

            migrationBuilder.CreateIndex(
                name: "ix_club_members_profile_id",
                schema: "community",
                table: "club_members",
                column: "profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_club_notifications_club_id",
                schema: "community",
                table: "club_notifications",
                column: "club_id");

            migrationBuilder.CreateIndex(
                name: "ix_club_notifications_profile_id_read_at",
                schema: "community",
                table: "club_notifications",
                columns: new[] { "profile_id", "read_at" });

            migrationBuilder.CreateIndex(
                name: "ix_clubs_category",
                schema: "community",
                table: "clubs",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "IX_clubs_created_by_profile_id",
                schema: "community",
                table: "clubs",
                column: "created_by_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_clubs_slug",
                schema: "community",
                table: "clubs",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_clubs_status",
                schema: "community",
                table: "clubs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_event_attendances_event_id_profile_id",
                schema: "community",
                table: "event_attendances",
                columns: new[] { "event_id", "profile_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_event_attendances_profile_id",
                schema: "community",
                table: "event_attendances",
                column: "profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_live_chat_messages_live_session_id_sent_at",
                schema: "community",
                table: "live_chat_messages",
                columns: new[] { "live_session_id", "sent_at" });

            migrationBuilder.CreateIndex(
                name: "IX_live_chat_messages_sender_profile_id",
                schema: "community",
                table: "live_chat_messages",
                column: "sender_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_live_sessions_club_id_scheduled_start_at",
                schema: "community",
                table: "live_sessions",
                columns: new[] { "club_id", "scheduled_start_at" });

            migrationBuilder.CreateIndex(
                name: "IX_live_sessions_event_id",
                schema: "community",
                table: "live_sessions",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "IX_moderation_logs_actor_profile_id",
                schema: "community",
                table: "moderation_logs",
                column: "actor_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_moderation_logs_club_id_created_at",
                schema: "community",
                table: "moderation_logs",
                columns: new[] { "club_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_moderation_logs_target_profile_id",
                schema: "community",
                table: "moderation_logs",
                column: "target_profile_id");

            migrationBuilder.AddForeignKey(
                name: "FK_posts_clubs_club_id",
                schema: "community",
                table: "posts",
                column: "club_id",
                principalSchema: "community",
                principalTable: "clubs",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_posts_clubs_club_id",
                schema: "community",
                table: "posts");

            migrationBuilder.DropTable(
                name: "club_categories",
                schema: "community");

            migrationBuilder.DropTable(
                name: "club_invitations",
                schema: "community");

            migrationBuilder.DropTable(
                name: "club_members",
                schema: "community");

            migrationBuilder.DropTable(
                name: "club_notifications",
                schema: "community");

            migrationBuilder.DropTable(
                name: "event_attendances",
                schema: "community");

            migrationBuilder.DropTable(
                name: "live_chat_messages",
                schema: "community");

            migrationBuilder.DropTable(
                name: "moderation_logs",
                schema: "community");

            migrationBuilder.DropTable(
                name: "live_sessions",
                schema: "community");

            migrationBuilder.DropTable(
                name: "club_events",
                schema: "community");

            migrationBuilder.DropTable(
                name: "clubs",
                schema: "community");

            migrationBuilder.DropIndex(
                name: "ix_posts_club_id_club_status",
                schema: "community",
                table: "posts");

            migrationBuilder.DropIndex(
                name: "ix_posts_club_id_created_at",
                schema: "community",
                table: "posts");

            migrationBuilder.DropColumn(
                name: "club_id",
                schema: "community",
                table: "posts");

            migrationBuilder.DropColumn(
                name: "club_status",
                schema: "community",
                table: "posts");

            migrationBuilder.DropColumn(
                name: "club_visibility",
                schema: "community",
                table: "posts");

            migrationBuilder.DropColumn(
                name: "featured",
                schema: "community",
                table: "posts");

            migrationBuilder.DropColumn(
                name: "scheduled_for",
                schema: "community",
                table: "posts");
        }
    }
}
