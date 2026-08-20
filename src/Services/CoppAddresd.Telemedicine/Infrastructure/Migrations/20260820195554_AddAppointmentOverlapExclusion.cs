using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Telemedicine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentOverlapExclusion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Extensión para el operador '=' sobre uuid dentro de la exclusión GiST.
            // El vector extension ya lo creó la app (mismo privilegio de usuario).
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            // Garantía REAL contra la doble reserva bajo concurrencia: dos citas
            // ACTIVAS del mismo profesional no pueden tener rangos de tiempo que
            // se solapen (tstzrange &&). Complementa el índice único parcial de
            // inicio exacto (ix_appointments_professional_start_active).
            migrationBuilder.Sql("""
                ALTER TABLE tele.telemedicine_appointments
                ADD CONSTRAINT ex_appointments_professional_no_overlap
                EXCLUDE USING gist (
                    professional_id WITH =,
                    tstzrange(scheduled_start, scheduled_end) WITH &&
                )
                WHERE (status IN ('Requested', 'Confirmed', 'InProgress'));
                """);

            // Una solicitud se convierte en UNA sola cita: índice único parcial
            // sobre request_id (anti doble confirmación ante reintentos).
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS ix_appointments_request_id
                ON tele.telemedicine_appointments (request_id)
                WHERE request_id IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE tele.telemedicine_appointments
                DROP CONSTRAINT IF EXISTS ex_appointments_professional_no_overlap;
                """);

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS tele.ix_appointments_request_id;
                """);
        }
    }
}
