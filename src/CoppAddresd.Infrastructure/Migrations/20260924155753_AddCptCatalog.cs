using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCptCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cpt_codes",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cpt_codes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cpt_codes_code",
                schema: "app",
                table: "cpt_codes",
                column: "code",
                unique: true);

            // Índices trigram para el autocomplete (patrón icd10_codes,
            // migración AddPatientCatalogs).
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS ix_cpt_codes_code_trgm
                    ON app.cpt_codes USING gin (code gin_trgm_ops);

                CREATE INDEX IF NOT EXISTS ix_cpt_codes_description_trgm
                    ON app.cpt_codes USING gin (description gin_trgm_ops);

                GRANT SELECT, INSERT, UPDATE, DELETE ON app.cpt_codes TO app_user;
                """);

            // Seed inicial curado (subset común de CPT 2024/2025 de EE. UU.):
            // E/M, preventivos, vacunas, laboratorio, imágenes, cardiología,
            // procedimientos menores, terapia física, telemedicina,
            // psicoterapia y nutrición médica (97802–97804). Idempotente por
            // code para permitir re-ejecuciones y crecer por script.
            migrationBuilder.Sql("""
                INSERT INTO app.cpt_codes (code, description) VALUES
                    ('99202', 'Visita de oficina u otro servicio ambulatorio, evaluación y manejo de nuevo paciente, nivel 2'),
                    ('99203', 'Visita de oficina nuevo paciente, nivel 3'),
                    ('99204', 'Visita de oficina nuevo paciente, nivel 4'),
                    ('99205', 'Visita de oficina nuevo paciente, nivel 5'),
                    ('99211', 'Visita de oficina paciente establecido, no requiere interacción directa con el médico'),
                    ('99212', 'Visita de oficina paciente establecido, nivel 2'),
                    ('99213', 'Visita de oficina paciente establecido, nivel 3'),
                    ('99214', 'Visita de oficina paciente establecido, nivel 4'),
                    ('99215', 'Visita de oficina paciente establecido, nivel 5'),
                    ('99231', 'Visita subsecuente de cuidado hospitalario, 25 minutos'),
                    ('99232', 'Visita subsecuente de cuidado hospitalario, 35 minutos'),
                    ('99233', 'Visita subsecuente de cuidado hospitalario, 50 minutos'),
                    ('99381', 'Examen preventivo inicial, nuevo paciente, infancia (edad 1-4)'),
                    ('99385', 'Examen preventivo inicial, nuevo paciente, edad 18-39'),
                    ('99395', 'Examen preventivo, paciente establecido, edad 18-39'),
                    ('99396', 'Examen preventivo, paciente establecido, edad 40-64'),
                    ('99397', 'Examen preventivo, paciente establecido, edad 65+'),
                    ('90460', 'Administración de inmunización hasta 8 años de edad vía oral o nasal'),
                    ('90686', 'Vacuna influenza inactivada intramuscular, trivalente o cuadrivalente, preservative-free'),
                    ('80053', 'Panel de química sanguínea general (CBC con diferencial opcional)'),
                    ('81001', 'Análisis de orina con microscopía automatizada'),
                    ('82947', 'Glucosa en sangre, cuantitativa'),
                    ('82962', 'Glucosa en sangre por monitor de glucosa (solo consumo en casa)'),
                    ('84443', 'Tiroxina T4 total'),
                    ('85025', 'Hemograma completo (CBC) con diferencial automatizado'),
                    ('85610', 'Tiempo de protrombina (PT)'),
                    ('87070', 'Cultivo bacteriano, cualquier fuente'),
                    ('87184', 'Determinación de sensibilidad antimicrobiana'),
                    ('71045', 'Radiografía de tórax, vista simple'),
                    ('71046', 'Radiografía de tórax, dos vistas'),
                    ('72148', 'RM de columna lumbar sin contraste'),
                    ('73000', 'Radiografía de hombro, vista simple'),
                    ('73630', 'Radiografía de tobillo, vista simple'),
                    ('93000', 'Electrocardiograma de 12 derivaciones con interpretación y reporte'),
                    ('93010', 'Electrocardiograma de 12 derivaciones, trazado solamente'),
                    ('93040', 'Trazado de electrocardiograma de ritmo'),
                    ('93306', 'Ecocardiografía Doppler transtorácica completa con interpretación'),
                    ('93351', 'Ecocardiografía completa en reposo con interpretación'),
                    ('12001', 'Sutura simple de herida superficial (2.5 cm o menor)'),
                    ('17110', 'Destrucción de lesiones benignas o premalignas, hasta 14 lesiones'),
                    ('20552', 'Inyección de puntos gatillo, 1-2 músculos'),
                    ('20610', 'Articulación mayor: arthrocentesis/inyección'),
                    ('97110', 'Ejercicios terapéuticos, cada 15 minutos'),
                    ('97140', 'Terapia manual, cada 15 minutos'),
                    ('99441', 'Consulta de telemedicina por teléfono, 5-10 minutos'),
                    ('99442', 'Consulta de telemedicina por teléfono, 11-20 minutos'),
                    ('99443', 'Consulta de telemedicina por teléfono, 21-30 minutos'),
                    ('90834', 'Psicoterapia individual, 45 minutos con el paciente'),
                    ('90837', 'Psicoterapia individual, 60 minutos con el paciente'),
                    ('97802', 'Evaluación y manejo inicial de nutrición médica, 15 minutos'),
                    ('97803', 'Reevaluación y intervención subsiguiente de nutrición médica, 15 minutos'),
                    ('97804', 'Evaluación grupal de nutrición médica, 2 o más individuos')
                ON CONFLICT (code) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS ix_cpt_codes_code_trgm;
                DROP INDEX IF EXISTS ix_cpt_codes_description_trgm;
                """);

            migrationBuilder.DropTable(
                name: "cpt_codes",
                schema: "app");
        }
    }
}
