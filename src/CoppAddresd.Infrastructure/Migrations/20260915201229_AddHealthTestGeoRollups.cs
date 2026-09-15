using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHealthTestGeoRollups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La base ya tenía índices equivalentes creados por SQL directo con
            // nombres estilo EF por defecto (IX_*): se eliminan para dejar un solo
            // índice por columna bajo la convención ix_* del modelo.
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS app.\"IX_patient_profiles_city_id\"");

            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS app.\"IX_patient_profiles_state_id\"");

            migrationBuilder.CreateIndex(
                name: "ix_patient_profiles_city_id",
                schema: "app",
                table: "patient_profiles",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "ix_patient_profiles_state_id",
                schema: "app",
                table: "patient_profiles",
                column: "state_id");

            migrationBuilder.CreateTable(
                name: "health_test_geo_rollups",
                schema: "app",
                columns: table => new
                {
                    city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    city_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    patients_count = table.Column<long>(type: "bigint", nullable: false),
                    evaluated_count = table.Column<long>(type: "bigint", nullable: false),
                    high_risk_count = table.Column<long>(type: "bigint", nullable: false),
                    active_alerts_count = table.Column<long>(type: "bigint", nullable: false),
                    avg_score_sum = table.Column<decimal>(type: "numeric(14,4)", nullable: false),
                    avg_score_count = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_test_geo_rollups", x => x.city_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_health_test_geo_rollups_state_code",
                schema: "app",
                table: "health_test_geo_rollups",
                column: "state_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "health_test_geo_rollups",
                schema: "app");

            migrationBuilder.DropIndex(
                name: "ix_health_test_geo_rollups_state_code",
                schema: "app",
                table: "health_test_geo_rollups");

            migrationBuilder.DropIndex(
                name: "ix_patient_profiles_city_id",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropIndex(
                name: "ix_patient_profiles_state_id",
                schema: "app",
                table: "patient_profiles");

            // Restaura los índices que existían en la base antes de esta migración.
            migrationBuilder.CreateIndex(
                name: "IX_patient_profiles_city_id",
                schema: "app",
                table: "patient_profiles",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "IX_patient_profiles_state_id",
                schema: "app",
                table: "patient_profiles",
                column: "state_id");
        }
    }
}
