using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateInitialBatteryInstrumentNames : Migration
    {
        /// <inheritdoc />
        /// <summary>
        /// Alinea nombre/descripción de los 9 instrumentos de la batería inicial
        /// con el contenido del HTML (el seed es aditivo: ON CONFLICT DO NOTHING no
        /// toca las filas existentes). Mismo nombre/descripción que emite
        /// <c>scripts/generate_health_tests_seed.py</c> para entornos nuevos.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE app.health_test_instruments
                SET name = 'Historia clínica',
                    description = 'Antecedentes, medicamentos y cómo te sientes físicamente.'
                WHERE code = 'historia-clinica';

                UPDATE app.health_test_instruments
                SET name = 'Temperamento',
                    description = 'Tu personalidad determina cómo te acompañamos en el programa.'
                WHERE code = 'temperamento';

                UPDATE app.health_test_instruments
                SET name = 'Nutrición',
                    description = 'Tus hábitos alimentarios y tu relación con la comida.'
                WHERE code = 'nutricional';

                UPDATE app.health_test_instruments
                SET name = 'Movimiento · AMAF',
                    description = 'Tu capacidad física actual determina el circuito que te asignamos.'
                WHERE code = 'movimiento';

                UPDATE app.health_test_instruments
                SET name = 'Sueño',
                    description = 'El sueño impacta directamente tu glucosa, tu peso y tu adherencia.'
                WHERE code = 'sueno';

                UPDATE app.health_test_instruments
                SET name = 'Adherencia · IAC',
                    description = 'Tu motivación real determina cómo te acompañamos.'
                WHERE code = 'iac-adresd';

                UPDATE app.health_test_instruments
                SET name = 'Riesgo cardiometabólico ORP',
                    description = 'Información clínica confidencial — solo la ve tu equipo médico.'
                WHERE code = 'orp';

                UPDATE app.health_test_instruments
                SET name = 'Estrés relacional · ERS',
                    description = 'El estrés en casa o en el trabajo es la barrera #1 de la adherencia.'
                WHERE code = 'ers';

                UPDATE app.health_test_instruments
                SET name = 'Propósito · ANTARES',
                    description = 'Las respuestas más importantes del programa. Sé completamente honesto/a.'
                WHERE code = 'bateria-antares';
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reversible por datos: restaurar los nombres/descripciones anteriores.
            migrationBuilder.Sql(
                """
                UPDATE app.health_test_instruments
                SET name = 'Historia clínica biológica', description = 'Antecedentes · Examen físico · Sistemas'
                WHERE code = 'historia-clinica';

                UPDATE app.health_test_instruments
                SET name = 'Test de temperamento', description = 'Sanguíneo · Colérico · Melancólico · Flemático'
                WHERE code = 'temperamento';

                UPDATE app.health_test_instruments
                SET name = 'Test nutricional y hábitos', description = 'Alimentación · Conducta · Motivación'
                WHERE code = 'nutricional';

                UPDATE app.health_test_instruments
                SET name = 'Movimiento y actividad física', description = 'AMAF · Nivel funcional · Capacidad'
                WHERE code = 'movimiento';

                UPDATE app.health_test_instruments
                SET name = 'Caracterización del sueño', description = 'Duración · Calidad · Hábitos · Riesgos'
                WHERE code = 'sueno';

                UPDATE app.health_test_instruments
                SET name = 'Índice de adherencia IAC-ADRESD', description = 'Motivación · Autoeficacia · Compromiso'
                WHERE code = 'iac-adresd';

                UPDATE app.health_test_instruments
                SET name = 'Riesgo cardiometabólico ORP', description = 'OMS · Obesidad · Complicaciones · Riesgo'
                WHERE code = 'orp';

                UPDATE app.health_test_instruments
                SET name = 'Test de estrés relacional ERS', description = 'Familia · Pareja · Trabajo · Entorno social'
                WHERE code = 'ers';

                UPDATE app.health_test_instruments
                SET name = 'Batería inicial completa ANTARES', description = 'PHS · Propósito · Mentalidad · Perfil final'
                WHERE code = 'bateria-antares';
                """
            );
        }
    }
}
