using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.WhatsApp.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccompanimentReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reminders",
                schema: "whatsapp",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    lease_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    repeat_every_minutes = table.Column<int>(type: "integer", nullable: false),
                    max_attempts = table.Column<int>(type: "integer", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cancellation_reason = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reminders", x => x.id);
                    table.CheckConstraint("ck_reminders_attempts", "attempts >= 0 AND max_attempts BETWEEN 1 AND 5 AND attempts <= max_attempts AND repeat_every_minutes >= 120");
                });

            migrationBuilder.CreateTable(
                name: "reminder_deliveries",
                schema: "whatsapp",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reminder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempt = table.Column<int>(type: "integer", nullable: false),
                    wamid = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    error_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    attempted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reminder_deliveries", x => x.id);
                    table.ForeignKey(
                        name: "FK_reminder_deliveries_reminders_reminder_id",
                        column: x => x.reminder_id,
                        principalSchema: "whatsapp",
                        principalTable: "reminders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reminder_deliveries_patient_id_kind_accepted_at",
                schema: "whatsapp",
                table: "reminder_deliveries",
                columns: new[] { "patient_id", "kind", "accepted_at" },
                filter: "accepted_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_reminder_deliveries_reminder_id_attempt",
                schema: "whatsapp",
                table: "reminder_deliveries",
                columns: new[] { "reminder_id", "attempt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reminders_assignment_id",
                schema: "whatsapp",
                table: "reminders",
                column: "assignment_id",
                filter: "assignment_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_reminders_next_attempt_at",
                schema: "whatsapp",
                table: "reminders",
                column: "next_attempt_at",
                filter: "state = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_reminders_patient_id_kind",
                schema: "whatsapp",
                table: "reminders",
                columns: new[] { "patient_id", "kind" },
                unique: true,
                filter: "state = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_reminders_source_key",
                schema: "whatsapp",
                table: "reminders",
                column: "source_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reminder_deliveries",
                schema: "whatsapp");

            migrationBuilder.DropTable(
                name: "reminders",
                schema: "whatsapp");
        }
    }
}
