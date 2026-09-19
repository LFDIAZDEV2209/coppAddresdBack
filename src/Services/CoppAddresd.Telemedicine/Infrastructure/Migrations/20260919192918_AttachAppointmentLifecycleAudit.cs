using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Telemedicine.Infrastructure.Migrations
{
    /// <summary>
    /// F4 — auditoría del ciclo de vida de la cita: adjunta el trigger
    /// <c>audit.audit_trigger_function</c> a <c>tele.appointments</c>,
    /// <c>tele.telemedicine_sessions</c> y <c>tele.virtual_rooms</c> (inicio/fin/
    /// reapertura/cancelación/reprogramación/no-show y creación/cambio de estado
    /// de sala y sesión). Condicional (el schema <c>audit</c> es del backend:
    /// en bases nuevas de tests se omite sin error) e idempotente (reintentos de
    /// deploy). Sin doble escritura desde la aplicación: la transición se lee de
    /// <c>changed_data</c> en <c>audit.activity_logs</c>. El actor del JWT llega
    /// por <c>AuditTriggerInterceptor</c> + la transacción explícita de
    /// <c>AppointmentRepository</c>; barrido y webhooks quedan como SYSTEM.
    /// </summary>
    public partial class AttachAppointmentLifecycleAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM pg_proc p
                        JOIN pg_namespace n ON n.oid = p.pronamespace
                        WHERE n.nspname = 'audit' AND p.proname = 'attach_table_audit')
                    THEN
                        IF NOT EXISTS (
                            SELECT 1 FROM pg_trigger
                            WHERE tgname = 'appointments_audit'
                              AND tgrelid = 'tele.appointments'::regclass)
                        THEN
                            PERFORM audit.attach_table_audit(
                                'tele', 'appointments', 'id', VARIADIC ARRAY[]::text[]);
                        END IF;

                        IF NOT EXISTS (
                            SELECT 1 FROM pg_trigger
                            WHERE tgname = 'telemedicine_sessions_audit'
                              AND tgrelid = 'tele.telemedicine_sessions'::regclass)
                        THEN
                            PERFORM audit.attach_table_audit(
                                'tele', 'telemedicine_sessions', 'id', VARIADIC ARRAY[]::text[]);
                        END IF;

                        IF NOT EXISTS (
                            SELECT 1 FROM pg_trigger
                            WHERE tgname = 'virtual_rooms_audit'
                              AND tgrelid = 'tele.virtual_rooms'::regclass)
                        THEN
                            PERFORM audit.attach_table_audit(
                                'tele', 'virtual_rooms', 'id', VARIADIC ARRAY[]::text[]);
                        END IF;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS appointments_audit ON tele.appointments;
                DROP TRIGGER IF EXISTS telemedicine_sessions_audit ON tele.telemedicine_sessions;
                DROP TRIGGER IF EXISTS virtual_rooms_audit ON tele.virtual_rooms;
                """);
        }
    }
}
