using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameAntaresBrandInAppData : Migration
    {
        /// <inheritdoc />
        /// <summary>
        /// Renombra la marca visible en datos ya sembrados (seed aditivo: ON CONFLICT
        /// DO NOTHING no toca filas existentes). Solo texto de display; los códigos
        /// técnicos (p. ej. 'bateria-antares') se mantienen.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE app.health_test_instruments
                SET name = replace(name, 'ANTARES', 'Copp Adresd')
                WHERE name LIKE '%ANTARES%';

                UPDATE app.health_test_instruments
                SET description = replace(description, 'ANTARES', 'Copp Adresd')
                WHERE description LIKE '%ANTARES%';

                UPDATE app.health_test_batteries
                SET name = replace(name, 'ANTARES', 'Copp Adresd')
                WHERE name LIKE '%ANTARES%';

                UPDATE app.health_test_batteries
                SET description = replace(description, 'ANTARES', 'Copp Adresd')
                WHERE description LIKE '%ANTARES%';

                UPDATE app.health_test_questions
                SET text = replace(text, 'ANTARES', 'Copp Adresd')
                WHERE text LIKE '%ANTARES%';

                UPDATE app.program_templates
                SET name = replace(name, 'ANTARES', 'Copp Adresd')
                WHERE name LIKE '%ANTARES%';

                UPDATE app.program_templates
                SET description = replace(description, 'ANTARES', 'Copp Adresd')
                WHERE description LIKE '%ANTARES%';
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reversible por datos: volver al nombre de marca anterior.
            migrationBuilder.Sql(
                """
                UPDATE app.health_test_instruments
                SET name = replace(name, 'Copp Adresd', 'ANTARES')
                WHERE name LIKE '%Copp Adresd%';

                UPDATE app.health_test_instruments
                SET description = replace(description, 'Copp Adresd', 'ANTARES')
                WHERE description LIKE '%Copp Adresd%';

                UPDATE app.health_test_batteries
                SET name = replace(name, 'Copp Adresd', 'ANTARES')
                WHERE name LIKE '%Copp Adresd%';

                UPDATE app.health_test_batteries
                SET description = replace(description, 'Copp Adresd', 'ANTARES')
                WHERE description LIKE '%Copp Adresd%';

                UPDATE app.health_test_questions
                SET text = replace(text, 'Copp Adresd', 'ANTARES')
                WHERE text LIKE '%Copp Adresd%';

                UPDATE app.program_templates
                SET name = replace(name, 'Copp Adresd', 'ANTARES')
                WHERE name LIKE '%Copp Adresd%';

                UPDATE app.program_templates
                SET description = replace(description, 'Copp Adresd', 'ANTARES')
                WHERE description LIKE '%Copp Adresd%';
                """
            );
        }
    }
}
