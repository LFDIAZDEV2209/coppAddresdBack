using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramProgressInterventions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "interventions",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    weakness_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "detected"),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "medium"),
                    assigned_to = table.Column<Guid>(type: "uuid", nullable: true),
                    recommended_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    accepted_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    patient_action = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    result = table.Column<string>(type: "text", nullable: true),
                    xp_awarded_total = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_interventions", x => x.id);
                    table.CheckConstraint("ck_interventions_status_values", "\"status\" IN ('detected', 'evaluated', 'recommended', 'accepted', 'in_progress', 'completed', 'reevaluation')");
                    table.CheckConstraint("ck_interventions_type_values", "\"type\" IN ('nutrition_adjustment', 'exercise_adjustment', 'psychological_support', 'telehealth_nutrition', 'telehealth_medical', 'telehealth_psychology', 'recovery_mission', 'plan_adaptation')");
                    table.ForeignKey(
                        name: "FK_interventions_patient_profiles_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_interventions_weaknesses_weakness_id",
                        column: x => x.weakness_id,
                        principalSchema: "app",
                        principalTable: "weaknesses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_interventions_patient_status",
                schema: "app",
                table: "interventions",
                columns: new[] { "patient_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_interventions_status",
                schema: "app",
                table: "interventions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_interventions_weakness_id",
                schema: "app",
                table: "interventions",
                column: "weakness_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "interventions",
                schema: "app");
        }
    }
}
