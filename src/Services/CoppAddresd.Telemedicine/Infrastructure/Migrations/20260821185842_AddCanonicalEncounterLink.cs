using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Telemedicine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCanonicalEncounterLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "encounter_id",
                schema: "tele",
                table: "clinical_encounters",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_clinical_encounters_encounter_id",
                schema: "tele",
                table: "clinical_encounters",
                column: "encounter_id");

            // FK hacia el encounter canónico del core (app.encounters), creada por
            // SQL fuera del modelo EF (igual que las FKs externas hacia auth.users):
            // este servicio es standalone y no conoce el tipo de la entidad. La
            // tabla app.encounters la crea el core con sus migraciones; esta FK
            // solo la referencia.
            //
            // CONDICIONAL (patrón AttachClinicalEncounterAudit): en instancias
            // compartidas donde el core ya aplicó sus migraciones, app.encounters
            // existe y la FK se crea. En bases aisladas del microservicio (tests de
            // integración coppaddresd_tele_test_*), el schema app no existe y la FK
            // se omite sin error. Idempotente para reintentos de despliegue.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.tables
                        WHERE table_schema = 'app' AND table_name = 'encounters')
                       AND NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'fk_clinical_encounters_canonical_encounter'
                          AND conrelid = 'tele.clinical_encounters'::regclass)
                    THEN
                        ALTER TABLE tele.clinical_encounters
                            ADD CONSTRAINT fk_clinical_encounters_canonical_encounter
                            FOREIGN KEY (encounter_id) REFERENCES app.encounters (id)
                            ON DELETE SET NULL;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE tele.clinical_encounters
                    DROP CONSTRAINT IF EXISTS fk_clinical_encounters_canonical_encounter;
                """);

            migrationBuilder.DropIndex(
                name: "ix_clinical_encounters_encounter_id",
                schema: "tele",
                table: "clinical_encounters");

            migrationBuilder.DropColumn(
                name: "encounter_id",
                schema: "tele",
                table: "clinical_encounters");
        }
    }
}
