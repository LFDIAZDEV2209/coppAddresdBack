using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <summary>
    /// Convierte los placeholders de las plantillas de notificación de llaves a
    /// corchetes: <c>{clave}</c> → <c>[clave]</c> (SPEC A13). Solo toca las claves
    /// soportadas por el renderizador; cualquier otra llave se deja intacta.
    /// Aplica a la plantilla vigente y a su historial de versiones. No hay cambio
    /// de esquema.
    /// </summary>
    public partial class ConvertNotificationPlaceholdersToBrackets : Migration
    {
        /// <summary>Claves soportadas por <c>HealthTestTemplateRenderer</c>.</summary>
        private static readonly string[] Keys =
        [
            "paciente",
            "documento",
            "test",
            "indicador",
            "valor",
            "umbral",
            "severidad",
            "accion",
            "profesional",
            "fecha",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[]
                     {
                         "health_test_notification_templates",
                         "health_test_notification_template_versions",
                     })
            {
                foreach (var key in Keys)
                {
                    var legacy = "{" + key + "}";
                    var current = "[" + key + "]";
                    migrationBuilder.Sql(
                        $"""
                        UPDATE app.{table}
                        SET body_template = replace(body_template, '{legacy}', '{current}')
                        WHERE body_template LIKE '%{legacy}%';
                        """);
                }
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[]
                     {
                         "health_test_notification_templates",
                         "health_test_notification_template_versions",
                     })
            {
                foreach (var key in Keys)
                {
                    var legacy = "{" + key + "}";
                    var current = "[" + key + "]";
                    migrationBuilder.Sql(
                        $"""
                        UPDATE app.{table}
                        SET body_template = replace(body_template, '{current}', '{legacy}')
                        WHERE body_template LIKE '%{current}%';
                        """);
                }
            }
        }
    }
}
