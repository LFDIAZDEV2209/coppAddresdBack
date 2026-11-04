using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramAssignmentRecoveryAndNotices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "program_assignment_notices",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    recommendation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title_es = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    title_en = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    message_es = table.Column<string>(type: "text", nullable: false),
                    message_en = table.Column<string>(type: "text", nullable: false),
                    link = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    read_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    delivered_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    push_completed = table.Column<bool>(type: "boolean", nullable: false),
                    chat_completed = table.Column<bool>(type: "boolean", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    lease_until = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_assignment_notices", x => x.id);
                    table.ForeignKey(
                        name: "FK_program_assignment_notices_program_assignment_recommendatio~",
                        column: x => x.recommendation_id,
                        principalSchema: "app",
                        principalTable: "program_assignment_recommendations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "program_assignment_recoveries",
                schema: "app",
                columns: table => new
                {
                    battery_assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_assignment_recoveries", x => x.battery_assignment_id);
                    table.ForeignKey(
                        name: "FK_program_assignment_recoveries_health_test_battery_assignmen~",
                        column: x => x.battery_assignment_id,
                        principalSchema: "app",
                        principalTable: "health_test_battery_assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_program_assignment_notices_pending",
                schema: "app",
                table: "program_assignment_notices",
                column: "next_attempt_at",
                filter: "delivered_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_program_assignment_notices_user_created",
                schema: "app",
                table: "program_assignment_notices",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "uq_program_assignment_notices_rec_user_kind",
                schema: "app",
                table: "program_assignment_notices",
                columns: new[] { "recommendation_id", "user_id", "kind" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "program_assignment_notices",
                schema: "app");

            migrationBuilder.DropTable(
                name: "program_assignment_recoveries",
                schema: "app");
        }
    }
}
