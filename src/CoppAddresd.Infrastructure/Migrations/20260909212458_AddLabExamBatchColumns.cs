using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLabExamBatchColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "batch_id",
                schema: "app",
                table: "clinical_measurements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_key",
                schema: "app",
                table: "clinical_measurements",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_clinical_measurements_batch_id",
                schema: "app",
                table: "clinical_measurements",
                column: "batch_id",
                filter: "batch_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_clinical_measurements_batch_id",
                schema: "app",
                table: "clinical_measurements");

            migrationBuilder.DropColumn(
                name: "batch_id",
                schema: "app",
                table: "clinical_measurements");

            migrationBuilder.DropColumn(
                name: "source_key",
                schema: "app",
                table: "clinical_measurements");
        }
    }
}
