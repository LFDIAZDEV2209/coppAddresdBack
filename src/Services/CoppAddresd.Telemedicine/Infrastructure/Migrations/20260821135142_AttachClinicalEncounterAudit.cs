using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Telemedicine.Infrastructure.Migrations
{
    /// <summary>
    /// Hardening (Fase 12): adjunta el trigger de auditoría del ERP al registro
    /// clínico del microservicio (<c>tele.clinical_encounters</c>, PHI). La
    /// infraestructura de auditoría (schema <c>audit</c> + funciones) vive en el
    /// backend y se crea con SUS migraciones; por eso este paso es CONDICIONAL:
    /// si el schema <c>audit</c> existe en la instancia compartida, se adjunta el
    /// trigger; si no (p. ej. base nueva de pruebas del microservicio), se omite
    /// sin error. Idempotente para reintentos de despliegue.
    /// </summary>
    public partial class AttachClinicalEncounterAudit : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM pg_proc p
                        JOIN pg_namespace n ON n.oid = p.pronamespace
                        WHERE n.nspname = 'audit' AND p.proname = 'attach_table_audit')
                       AND NOT EXISTS (
                        SELECT 1 FROM pg_trigger
                        WHERE tgname = 'clinical_encounters_audit'
                          AND tgrelid = 'tele.clinical_encounters'::regclass)
                    THEN
                        -- old_data/new_data/changed_data incluyen el jsonb clínico
                        -- (notas + clinical_data). VARIADIC explícito: con cero
                        -- columnas excluidas PG no resuelve la llamada de 3 args
                        -- (literales unknown vs variadic text[]).
                        PERFORM audit.attach_table_audit(
                            'tele', 'clinical_encounters', 'id', VARIADIC ARRAY[]::text[]);
                    END IF;
                END $$;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS clinical_encounters_audit ON tele.clinical_encounters;
                """);
        }
    }
}