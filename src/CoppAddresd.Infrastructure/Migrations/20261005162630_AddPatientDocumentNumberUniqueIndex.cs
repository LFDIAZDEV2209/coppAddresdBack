using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientDocumentNumberUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Limpieza de duplicados previos: conserva la fila vinculada a un
            // usuario; si ninguna lo está, la más antigua. El resto se marca
            // como eliminada antes de crear el índice único.
            migrationBuilder.Sql(
                @"WITH ranked AS (
                    SELECT id,
                           row_number() OVER (
                               PARTITION BY lower(btrim(document_number))
                               ORDER BY (user_id IS NOT NULL) DESC, created_at ASC, id ASC
                           ) AS rn
                    FROM app.patient_profiles
                    WHERE deleted_at IS NULL
                      AND document_number IS NOT NULL
                      AND btrim(document_number) <> ''
                )
                UPDATE app.patient_profiles AS p
                SET deleted_at = now(),
                    updated_at = now()
                FROM ranked
                WHERE p.id = ranked.id AND ranked.rn > 1;"
            );

            migrationBuilder.Sql(
                @"CREATE UNIQUE INDEX ix_patient_profiles_document_number_unique
                    ON app.patient_profiles (lower(btrim(document_number)))
                    WHERE deleted_at IS NULL
                      AND document_number IS NOT NULL
                      AND btrim(document_number) <> '';"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS ix_patient_profiles_document_number_unique;"
            );
        }
    }
}
