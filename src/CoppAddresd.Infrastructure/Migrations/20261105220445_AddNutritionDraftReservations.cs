using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNutritionDraftReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotente para permitir revertir el binario y reaplicar sin borrar reservas.
            // Tabla nueva: estos índices no bloquean tablas clínicas existentes.
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS app.nutrition_draft_reservations (
                    id uuid PRIMARY KEY, patient_id uuid NOT NULL REFERENCES app.patient_profiles(id) ON DELETE RESTRICT,
                    battery_assignment_id uuid NOT NULL, state varchar(20) NOT NULL,
                    reason varchar(100), attempts integer NOT NULL, claim_token uuid,
                    lease_until timestamptz, next_attempt_at timestamptz NOT NULL,
                    created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL,
                    prescription_id uuid, fallback boolean NOT NULL,
                    CONSTRAINT ck_nutrition_draft_state CHECK (state IN ('pending','generating','ready','failed'))
                );
                CREATE INDEX IF NOT EXISTS ix_nutrition_draft_pending ON app.nutrition_draft_reservations(state, next_attempt_at);
                CREATE UNIQUE INDEX IF NOT EXISTS uq_nutrition_draft_patient_battery ON app.nutrition_draft_reservations(patient_id, battery_assignment_id);
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid='app.nutrition_draft_reservations'::regclass
                        AND tgname='audit_nutrition_draft_reservations') THEN
                        CREATE TRIGGER audit_nutrition_draft_reservations
                        AFTER INSERT OR UPDATE OR DELETE ON app.nutrition_draft_reservations
                        FOR EACH ROW EXECUTE FUNCTION audit.audit_trigger_function('id', 'claim_token');
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback no destructivo: volver al binario anterior conserva reservas e historia.
            // Retirada manual posterior, solo con respaldo y autorización:
            // DROP TABLE app.nutrition_draft_reservations;
        }
    }
}
