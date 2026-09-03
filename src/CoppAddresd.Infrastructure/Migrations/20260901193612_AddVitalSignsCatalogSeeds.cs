using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVitalSignsCatalogSeeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Seed de catálogos base para el servidor (alérgenos, ICD-10,
            // medicamentos, unidades, métricas, rangos de referencia, la
            // organización raíz Medicare y aseguradoras), incluidas las filas
            // nuevas de vital-signs-tracking: unidad 'celsius', métricas
            // 'o2_saturation' y 'temperature_c' y el rango de referencia de SpO2.
            // Idempotente (ON CONFLICT DO NOTHING / guard WHERE NOT EXISTS).
            // El archivo regenerado por scripts/generate_server_catalogs_seed.py
            // es el recurso embebido; este migration lo (re)aplica en el pipeline
            // de migración del backend.
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
            var assembly = typeof(AddVitalSignsCatalogSeeds).Assembly;
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
