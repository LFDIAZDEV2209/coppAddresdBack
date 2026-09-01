using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceInitialBatterySeed : Migration
    {
        /// <inheritdoc />
        /// <summary>
        /// Reemplaza el contenido de la batería inicial ANTARES por el seed
        /// regenerado (contenido fiel a ANTARES_Tests_Perfil_Salud (1).html).
        /// Elimina en orden FK-safe evaluaciones/respuestas/asignaciones y las
        /// preguntas/opciones/rangos de la v1 de los 9 instrumentos, y vuelve a
        /// sembrar el catálogo desde el recurso embebido (idempotente).
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                -- Reemplazo de la batería inicial: borra en orden FK-safe el contenido
                -- de los 9 instrumentos (evaluaciones, respuestas, asignaciones,
                -- preguntas, opciones, rangos) para volver a sembrarlo con el seed
                -- regenerado. Los instrumentos/versiones/batería/indicadores/reglas
                -- conservan su identidad (mismos códigos).
                DELETE FROM app.health_test_alerts
                WHERE result_id IN (
                    SELECT r.id
                    FROM app.health_test_results r
                    JOIN app.health_test_evaluations e ON e.id = r.evaluation_id
                    JOIN app.health_test_versions v ON v.id = e.version_id
                    JOIN app.health_test_instruments i ON i.id = v.instrument_id
                    WHERE i.code IN (
                        'historia-clinica', 'temperamento', 'nutricional', 'movimiento',
                        'sueno', 'iac-adresd', 'orp', 'ers', 'bateria-antares'
                    )
                );

                DELETE FROM app.health_test_comments
                WHERE evaluation_id IN (
                    SELECT e.id
                    FROM app.health_test_evaluations e
                    JOIN app.health_test_versions v ON v.id = e.version_id
                    JOIN app.health_test_instruments i ON i.id = v.instrument_id
                    WHERE i.code IN (
                        'historia-clinica', 'temperamento', 'nutricional', 'movimiento',
                        'sueno', 'iac-adresd', 'orp', 'ers', 'bateria-antares'
                    )
                );

                DELETE FROM app.health_test_results
                WHERE evaluation_id IN (
                    SELECT e.id
                    FROM app.health_test_evaluations e
                    JOIN app.health_test_versions v ON v.id = e.version_id
                    JOIN app.health_test_instruments i ON i.id = v.instrument_id
                    WHERE i.code IN (
                        'historia-clinica', 'temperamento', 'nutricional', 'movimiento',
                        'sueno', 'iac-adresd', 'orp', 'ers', 'bateria-antares'
                    )
                );

                DELETE FROM app.health_test_responses
                WHERE evaluation_id IN (
                    SELECT e.id
                    FROM app.health_test_evaluations e
                    JOIN app.health_test_versions v ON v.id = e.version_id
                    JOIN app.health_test_instruments i ON i.id = v.instrument_id
                    WHERE i.code IN (
                        'historia-clinica', 'temperamento', 'nutricional', 'movimiento',
                        'sueno', 'iac-adresd', 'orp', 'ers', 'bateria-antares'
                    )
                );

                DELETE FROM app.health_test_evaluations
                WHERE version_id IN (
                    SELECT v.id
                    FROM app.health_test_versions v
                    JOIN app.health_test_instruments i ON i.id = v.instrument_id
                    WHERE i.code IN (
                        'historia-clinica', 'temperamento', 'nutricional', 'movimiento',
                        'sueno', 'iac-adresd', 'orp', 'ers', 'bateria-antares'
                    )
                );

                DELETE FROM app.health_test_assignments
                WHERE version_id IN (
                    SELECT v.id
                    FROM app.health_test_versions v
                    JOIN app.health_test_instruments i ON i.id = v.instrument_id
                    WHERE i.code IN (
                        'historia-clinica', 'temperamento', 'nutricional', 'movimiento',
                        'sueno', 'iac-adresd', 'orp', 'ers', 'bateria-antares'
                    )
                );

                DELETE FROM app.health_test_battery_assignments
                WHERE battery_id IN (
                    SELECT id FROM app.health_test_batteries WHERE code = 'bateria-inicial'
                );

                DELETE FROM app.health_test_score_ranges
                WHERE version_id IN (
                    SELECT v.id
                    FROM app.health_test_versions v
                    JOIN app.health_test_instruments i ON i.id = v.instrument_id
                    WHERE i.code IN (
                        'historia-clinica', 'temperamento', 'nutricional', 'movimiento',
                        'sueno', 'iac-adresd', 'orp', 'ers', 'bateria-antares'
                    )
                );

                DELETE FROM app.health_test_answer_options
                WHERE question_id IN (
                    SELECT q.id
                    FROM app.health_test_questions q
                    JOIN app.health_test_versions v ON v.id = q.version_id
                    JOIN app.health_test_instruments i ON i.id = v.instrument_id
                    WHERE i.code IN (
                        'historia-clinica', 'temperamento', 'nutricional', 'movimiento',
                        'sueno', 'iac-adresd', 'orp', 'ers', 'bateria-antares'
                    )
                );

                DELETE FROM app.health_test_questions
                WHERE version_id IN (
                    SELECT v.id
                    FROM app.health_test_versions v
                    JOIN app.health_test_instruments i ON i.id = v.instrument_id
                    WHERE i.code IN (
                        'historia-clinica', 'temperamento', 'nutricional', 'movimiento',
                        'sueno', 'iac-adresd', 'orp', 'ers', 'bateria-antares'
                    )
                );
                """
            );
            // Siembra el catálogo regenerado (mismo recurso embebido, ahora con el
            // contenido del HTML). Idempotente (ON CONFLICT DO NOTHING).
            migrationBuilder.Sql(ReadSeedScript());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // El reemplazo es aditivo e irreversible por diseño: el contenido
            // anterior de la batería (evaluaciones demo y preguntas viejas) ya no
            // existe para restaurar. Down intencionalmente vacío.
        }

        /// <summary>Lee el seed del catálogo embebido (recurso EmbeddedResource).</summary>
        private static string ReadSeedScript()
        {
            var assembly = typeof(ReplaceInitialBatterySeed).Assembly;
            using var stream =
                assembly.GetManifestResourceStream(
                    "CoppAddresd.Infrastructure.Migrations.Seed.AddHealthTestsCatalogSeed.sql"
                )
                ?? throw new InvalidOperationException(
                    "Recurso embebido 'AddHealthTestsCatalogSeed.sql' no encontrado."
                );
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
