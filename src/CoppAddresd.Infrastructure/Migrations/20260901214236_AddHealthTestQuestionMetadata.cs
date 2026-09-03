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
            migrationBuilder.Sql(
                """
                ALTER TABLE app.health_test_questions ADD COLUMN IF NOT EXISTS unit character varying(20);
                ALTER TABLE app.health_test_questions ADD COLUMN IF NOT EXISTS min_value numeric;
                ALTER TABLE app.health_test_questions ADD COLUMN IF NOT EXISTS max_value numeric;
                ALTER TABLE app.health_test_questions ADD COLUMN IF NOT EXISTS default_value numeric;
                ALTER TABLE app.health_test_questions ADD COLUMN IF NOT EXISTS min_label character varying(40);
                ALTER TABLE app.health_test_questions ADD COLUMN IF NOT EXISTS max_label character varying(40);
                ALTER TABLE app.health_test_questions ADD COLUMN IF NOT EXISTS hint text;
                """
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
