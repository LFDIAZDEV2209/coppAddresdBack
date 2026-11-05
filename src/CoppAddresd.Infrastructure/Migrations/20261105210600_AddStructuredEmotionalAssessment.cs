using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredEmotionalAssessment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "motivation_score",
                schema: "app",
                table: "emotional_records",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sleep_device_id",
                schema: "app",
                table: "emotional_records",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "sleep_minutes",
                schema: "app",
                table: "emotional_records",
                type: "numeric(7,2)",
                precision: 7,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "sleep_night_date",
                schema: "app",
                table: "emotional_records",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sleep_quality",
                schema: "app",
                table: "emotional_records",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "sleep_recorded_at",
                schema: "app",
                table: "emotional_records",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sleep_source",
                schema: "app",
                table: "emotional_records",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "stress_score",
                schema: "app",
                table: "emotional_records",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "motivation_score",
                schema: "app",
                table: "emotional_records");

            migrationBuilder.DropColumn(
                name: "sleep_device_id",
                schema: "app",
                table: "emotional_records");

            migrationBuilder.DropColumn(
                name: "sleep_minutes",
                schema: "app",
                table: "emotional_records");

            migrationBuilder.DropColumn(
                name: "sleep_night_date",
                schema: "app",
                table: "emotional_records");

            migrationBuilder.DropColumn(
                name: "sleep_quality",
                schema: "app",
                table: "emotional_records");

            migrationBuilder.DropColumn(
                name: "sleep_recorded_at",
                schema: "app",
                table: "emotional_records");

            migrationBuilder.DropColumn(
                name: "sleep_source",
                schema: "app",
                table: "emotional_records");

            migrationBuilder.DropColumn(
                name: "stress_score",
                schema: "app",
                table: "emotional_records");
        }
    }
}
