using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHealthTestQuestionMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "unit",
                schema: "app",
                table: "health_test_questions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true
            );

            migrationBuilder.AddColumn<decimal>(
                name: "min_value",
                schema: "app",
                table: "health_test_questions",
                type: "numeric",
                nullable: true
            );

            migrationBuilder.AddColumn<decimal>(
                name: "max_value",
                schema: "app",
                table: "health_test_questions",
                type: "numeric",
                nullable: true
            );

            migrationBuilder.AddColumn<decimal>(
                name: "default_value",
                schema: "app",
                table: "health_test_questions",
                type: "numeric",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "min_label",
                schema: "app",
                table: "health_test_questions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "max_label",
                schema: "app",
                table: "health_test_questions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "hint",
                schema: "app",
                table: "health_test_questions",
                type: "text",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "hint",
                schema: "app",
                table: "health_test_questions"
            );

            migrationBuilder.DropColumn(
                name: "max_label",
                schema: "app",
                table: "health_test_questions"
            );

            migrationBuilder.DropColumn(
                name: "min_label",
                schema: "app",
                table: "health_test_questions"
            );

            migrationBuilder.DropColumn(
                name: "default_value",
                schema: "app",
                table: "health_test_questions"
            );

            migrationBuilder.DropColumn(
                name: "max_value",
                schema: "app",
                table: "health_test_questions"
            );

            migrationBuilder.DropColumn(
                name: "min_value",
                schema: "app",
                table: "health_test_questions"
            );

            migrationBuilder.DropColumn(
                name: "unit",
                schema: "app",
                table: "health_test_questions"
            );
        }
    }
}
