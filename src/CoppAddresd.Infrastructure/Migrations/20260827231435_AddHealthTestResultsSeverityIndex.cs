using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHealthTestResultsSeverityIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Índice compuesto para la agregación del dashboard por severidad
            // (CountEvaluationsBySeverityAsync / highRisk): con 800k resultados
            // la query sin índice hace Parallel Seq Scan (~121ms). Con este
            // índice compuesto (result_type = igualdad, severity = igualdad)
            // se resuelve con Index Only Scan. Documentado en
            // docs/database/indexes.md.
            migrationBuilder.Sql(
                """
                CREATE INDEX ix_health_test_results_type_severity
                ON app.health_test_results (result_type, severity);
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS app.ix_health_test_results_type_severity;
                """
            );
        }
    }
}
