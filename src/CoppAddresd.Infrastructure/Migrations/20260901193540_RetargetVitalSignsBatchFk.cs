using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RetargetVitalSignsBatchFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_task_completions_vital_signs_vital_signs_batch_id",
                schema: "app",
                table: "task_completions");

            migrationBuilder.AddForeignKey(
                name: "FK_task_completions_clinical_measurements_vital_signs_batch_id",
                schema: "app",
                table: "task_completions",
                column: "vital_signs_batch_id",
                principalSchema: "app",
                principalTable: "clinical_measurements",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_task_completions_clinical_measurements_vital_signs_batch_id",
                schema: "app",
                table: "task_completions");

            migrationBuilder.AddForeignKey(
                name: "FK_task_completions_vital_signs_vital_signs_batch_id",
                schema: "app",
                table: "task_completions",
                column: "vital_signs_batch_id",
                principalSchema: "app",
                principalTable: "vital_signs",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
