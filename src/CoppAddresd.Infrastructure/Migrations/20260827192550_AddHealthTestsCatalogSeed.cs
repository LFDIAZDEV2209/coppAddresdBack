using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHealthTestsCatalogSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Asegura que las columnas de metadata existan en health_test_questions
            // independientemente del orden de ejecución de la migración embebida.
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

            migrationBuilder.Sql(ReadSeedScript());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // El seed es aditivo e idempotente: no se revierte (borrar el
            // catálogo rompería FK con asignaciones/evaluaciones). Down
            // intencionalmente vacío.
        }

        /// <summary>Lee el seed del catálogo embebido (recurso EmbeddedResource).</summary>
        private static string ReadSeedScript()
        {
            var assembly = typeof(AddHealthTestsCatalogSeed).Assembly;
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
