using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixEmotionalFkRestrict : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_emotional_records_program_enrollments_program_enrollment_id~",
                schema: "app",
                table: "emotional_records");

            migrationBuilder.AddForeignKey(
                name: "FK_emotional_records_program_enrollments_program_enrollment_id~",
                schema: "app",
                table: "emotional_records",
                columns: new[] { "program_enrollment_id", "patient_id" },
                principalSchema: "app",
                principalTable: "program_enrollments",
                principalColumns: new[] { "id", "patient_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_emotional_records_program_enrollments_program_enrollment_id~",
                schema: "app",
                table: "emotional_records");

            migrationBuilder.AddForeignKey(
                name: "FK_emotional_records_program_enrollments_program_enrollment_id~",
                schema: "app",
                table: "emotional_records",
                columns: new[] { "program_enrollment_id", "patient_id" },
                principalSchema: "app",
                principalTable: "program_enrollments",
                principalColumns: new[] { "id", "patient_id" },
                onDelete: ReferentialAction.SetNull);
        }
    }
}
