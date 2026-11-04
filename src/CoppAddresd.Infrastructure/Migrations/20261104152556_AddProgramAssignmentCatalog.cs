using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramAssignmentCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_program_enrollments_current_week_positive",
                schema: "app",
                table: "program_enrollments");

            migrationBuilder.CreateTable(
                name: "program_assignment_recommendations",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    profile_snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    proposal = table.Column<string>(type: "jsonb", nullable: false),
                    narrative_es = table.Column<string>(type: "text", nullable: true),
                    narrative_en = table.Column<string>(type: "text", nullable: true),
                    state = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false, defaultValue: "proposed"),
                    engine_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    model_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    applied_by = table.Column<Guid>(type: "uuid", nullable: true),
                    dismissed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    applied_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    dismissed_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_assignment_recommendations", x => x.id);
                    table.CheckConstraint("ck_program_assignment_recommendations_state", "state IN ('proposed', 'pending-narrative', 'applied', 'edited', 'dismissed', 'fallback')");
                    table.ForeignKey(
                        name: "FK_program_assignment_recommendations_patient_profiles_patient~",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_program_assignment_recommendations_program_enrollments_enro~",
                        columns: x => new { x.enrollment_id, x.patient_id },
                        principalSchema: "app",
                        principalTable: "program_enrollments",
                        principalColumns: new[] { "id", "patient_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "program_conditions",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name_es = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    icd10_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    criteria = table.Column<string>(type: "jsonb", nullable: false),
                    source_ref = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_conditions", x => x.id);
                    table.UniqueConstraint("uq_program_conditions_code", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "program_assignment_rules",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    condition_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    criteria = table.Column<string>(type: "jsonb", nullable: false),
                    target = table.Column<string>(type: "jsonb", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    weight = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false, defaultValue: 1m),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_assignment_rules", x => x.id);
                    table.ForeignKey(
                        name: "FK_program_assignment_rules_program_conditions_condition_code",
                        column: x => x.condition_code,
                        principalSchema: "app",
                        principalTable: "program_conditions",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_program_enrollments_current_week_positive",
                schema: "app",
                table: "program_enrollments",
                sql: "(\"status\" = 'PendingAssignment' AND \"current_week_number\" = 0) OR (\"status\" <> 'PendingAssignment' AND \"current_week_number\" >= 1)");

            migrationBuilder.CreateIndex(
                name: "ix_program_assignment_recommendations_enrollment_patient",
                schema: "app",
                table: "program_assignment_recommendations",
                columns: new[] { "enrollment_id", "patient_id" });

            migrationBuilder.CreateIndex(
                name: "ix_program_assignment_recommendations_patient_id",
                schema: "app",
                table: "program_assignment_recommendations",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_program_assignment_recommendations_state_created_at",
                schema: "app",
                table: "program_assignment_recommendations",
                columns: new[] { "state", "created_at" });

            migrationBuilder.CreateIndex(
                name: "uq_program_assignment_recommendations_enrollment",
                schema: "app",
                table: "program_assignment_recommendations",
                column: "enrollment_id",
                unique: true,
                filter: "state <> 'dismissed'");

            migrationBuilder.CreateIndex(
                name: "ix_program_assignment_rules_code",
                schema: "app",
                table: "program_assignment_rules",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_program_assignment_rules_condition_code",
                schema: "app",
                table: "program_assignment_rules",
                column: "condition_code");
            // Seed aditivo e idempotente por código, sin sobrescribir cambios curados.
            migrationBuilder.Sql(ReadSeedScript());

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "program_assignment_recommendations",
                schema: "app");

            migrationBuilder.DropTable(
                name: "program_assignment_rules",
                schema: "app");

            migrationBuilder.DropTable(
                name: "program_conditions",
                schema: "app");

            migrationBuilder.DropCheckConstraint(
                name: "ck_program_enrollments_current_week_positive",
                schema: "app",
                table: "program_enrollments");

            migrationBuilder.AddCheckConstraint(
                name: "ck_program_enrollments_current_week_positive",
                schema: "app",
                table: "program_enrollments",
                sql: "\"current_week_number\" >= 1");
        }
        /// <summary>Lee el catálogo clínico y la matriz v1 embebidos en esta migración.</summary>
        internal static string ReadSeedScript()
        {
            using var stream = typeof(AddProgramAssignmentCatalog).Assembly.GetManifestResourceStream(
                "CoppAddresd.Infrastructure.Migrations.Seed.AddProgramAssignmentCatalog.sql")
                ?? throw new InvalidOperationException("No se encontró el seed de asignación del programa.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

    }
}
