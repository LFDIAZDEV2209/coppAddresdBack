using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServerCatalogSeeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Seed de catálogos base para el servidor (alérgenos, ICD-10,
            // medicamentos, unidades, métricas, rangos de referencia, la
            // organización raíz Medicare y aseguradoras). Idempotente
            // (ON CONFLICT DO NOTHING / guard WHERE NOT EXISTS).
            migrationBuilder.Sql(ReadSeedScript());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // El seed es aditivo e idempotente: no se revierte (eliminar datos
            // rompería FK con datos de pacientes). Down intencionalmente vacío.
        }

        /// <summary>Lee el seed de catálogos embebido (recurso EmbeddedResource).</summary>
        private static string ReadSeedScript()
        {
            var assembly = typeof(AddServerCatalogSeeds).Assembly;
            using var stream =
                assembly.GetManifestResourceStream(
                    "CoppAddresd.Infrastructure.Migrations.Seed.AddServerCatalogs.sql"
                )
                ?? throw new InvalidOperationException(
                    "Recurso embebido 'AddServerCatalogs.sql' no encontrado."
                );
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
