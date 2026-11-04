using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNutritionReviewWorkflowAndEmergencyContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "emergency_contact",
                schema: "app",
                table: "patient_profiles",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "approved_at",
                schema: "app",
                table: "nutrition_plans",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "approved_by",
                schema: "app",
                table: "nutrition_plans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "assessment_snapshot",
                schema: "app",
                table: "nutrition_plans",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "generation_attempts",
                schema: "app",
                table: "nutrition_plans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "generation_key",
                schema: "app",
                table: "nutrition_plans",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "generation_lease_until",
                schema: "app",
                table: "nutrition_plans",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "generation_next_attempt_at",
                schema: "app",
                table: "nutrition_plans",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "generation_status",
                schema: "app",
                table: "nutrition_plans",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "review_status",
                schema: "app",
                table: "nutrition_plans",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "pending_review");

            migrationBuilder.AddColumn<int>(
                name: "revision",
                schema: "app",
                table: "nutrition_plans",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "app",
                table: "nutrition_plans",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "nutrition_plan_review_events",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    snapshot = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nutrition_plan_review_events", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_nutrition_plans_generation_queue",
                schema: "app",
                table: "nutrition_plans",
                columns: new[] { "generation_status", "generation_next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "uq_nutrition_plans_generation_key",
                schema: "app",
                table: "nutrition_plans",
                column: "generation_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_nutrition_plan_review_events_plan_time",
                schema: "app",
                table: "nutrition_plan_review_events",
                columns: new[] { "plan_id", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "nutrition_plan_review_events",
                schema: "app");

            migrationBuilder.DropIndex(
                name: "ix_nutrition_plans_generation_queue",
                schema: "app",
                table: "nutrition_plans");

            migrationBuilder.DropIndex(
                name: "uq_nutrition_plans_generation_key",
                schema: "app",
                table: "nutrition_plans");

            migrationBuilder.DropColumn(
                name: "approved_at",
                schema: "app",
                table: "nutrition_plans");

            migrationBuilder.DropColumn(
                name: "approved_by",
                schema: "app",
                table: "nutrition_plans");

            migrationBuilder.DropColumn(
                name: "assessment_snapshot",
                schema: "app",
                table: "nutrition_plans");

            migrationBuilder.DropColumn(
                name: "generation_attempts",
                schema: "app",
                table: "nutrition_plans");

            migrationBuilder.DropColumn(
                name: "generation_key",
                schema: "app",
                table: "nutrition_plans");

            migrationBuilder.DropColumn(
                name: "generation_lease_until",
                schema: "app",
                table: "nutrition_plans");

            migrationBuilder.DropColumn(
                name: "generation_next_attempt_at",
                schema: "app",
                table: "nutrition_plans");

            migrationBuilder.DropColumn(
                name: "generation_status",
                schema: "app",
                table: "nutrition_plans");

            migrationBuilder.DropColumn(
                name: "review_status",
                schema: "app",
                table: "nutrition_plans");

            migrationBuilder.DropColumn(
                name: "revision",
                schema: "app",
                table: "nutrition_plans");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "app",
                table: "nutrition_plans");

            migrationBuilder.AlterColumn<string>(
                name: "emergency_contact",
                schema: "app",
                table: "patient_profiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
