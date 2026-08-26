using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Community.Migrations
{
    /// <inheritdoc />
    public partial class AddChatGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "recipient_profile_id",
                schema: "community",
                table: "messages",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "conversation_id",
                schema: "community",
                table: "messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "chat_groups",
                schema: "community",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_by_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_groups", x => x.id);
                    table.ForeignKey(
                        name: "FK_chat_groups_profiles_created_by_profile_id",
                        column: x => x.created_by_profile_id,
                        principalSchema: "community",
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "chat_group_members",
                schema: "community",
                columns: table => new
                {
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    joined_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_group_members", x => new { x.group_id, x.profile_id });
                    table.ForeignKey(
                        name: "FK_chat_group_members_chat_groups_group_id",
                        column: x => x.group_id,
                        principalSchema: "community",
                        principalTable: "chat_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_chat_group_members_profiles_profile_id",
                        column: x => x.profile_id,
                        principalSchema: "community",
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_messages_conversation_created",
                schema: "community",
                table: "messages",
                columns: new[] { "conversation_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_chat_group_members_profile_id",
                schema: "community",
                table: "chat_group_members",
                column: "profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_chat_groups_created_by_profile_id",
                schema: "community",
                table: "chat_groups",
                column: "created_by_profile_id");

            migrationBuilder.AddForeignKey(
                name: "FK_messages_chat_groups_conversation_id",
                schema: "community",
                table: "messages",
                column: "conversation_id",
                principalSchema: "community",
                principalTable: "chat_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_messages_chat_groups_conversation_id",
                schema: "community",
                table: "messages");

            migrationBuilder.DropTable(
                name: "chat_group_members",
                schema: "community");

            migrationBuilder.DropTable(
                name: "chat_groups",
                schema: "community");

            migrationBuilder.DropIndex(
                name: "ix_messages_conversation_created",
                schema: "community",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "conversation_id",
                schema: "community",
                table: "messages");

            migrationBuilder.AlterColumn<Guid>(
                name: "recipient_profile_id",
                schema: "community",
                table: "messages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
