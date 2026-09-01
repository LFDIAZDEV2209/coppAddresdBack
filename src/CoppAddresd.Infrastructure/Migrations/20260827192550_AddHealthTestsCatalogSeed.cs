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
            // Seed del módulo Tests de Salud: batería inicial ANTARES (9
            // instrumentos, preguntas, opciones, rangos, indicadores y reglas
            // de alerta). Idempotente (ON CONFLICT DO NOTHING sobre claves únicas).
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
