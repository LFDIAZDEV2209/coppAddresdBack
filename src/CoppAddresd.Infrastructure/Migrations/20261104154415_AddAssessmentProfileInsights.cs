using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssessmentProfileInsights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "assessment_profile_insights",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    source_revision = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    language = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    content_json = table.Column<string>(type: "jsonb", nullable: false),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assessment_profile_insights", x => x.id);
                    table.ForeignKey(
                        name: "FK_assessment_profile_insights_patient_profiles_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "uq_assessment_insight_source_language",
                schema: "app",
                table: "assessment_profile_insights",
                columns: new[] { "patient_id", "source_revision", "language" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assessment_profile_insights",
                schema: "app");
        }
    }
}
