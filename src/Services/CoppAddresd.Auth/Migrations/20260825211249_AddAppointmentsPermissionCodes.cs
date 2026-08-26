using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Auth.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentsPermissionCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fase 1 del rename Telemedicine.* → Appointments.*: siembra los
            // códigos NUEVOS en auth.Permissions para la transición dual-emit.
            // Idempotente: WHERE NOT EXISTS sobre Code (los códigos legados
            // Telemedicine.* NO se tocan; permanecen deprecados pero funcionales).
            // Name/Module replican PermissionSeeder.FormatPermissionName para
            // mantener consistencia con el seeder de arranque (que además los
            // crearía si esta migración no se aplicó).
            var rows = new (string Code, string Name)[]
            {
                ("Appointments.RequestsCreate", "RequestsCreate appointments"),
                ("Appointments.RequestsView", "RequestsView appointments"),
                ("Appointments.RequestsConfirm", "RequestsConfirm appointments"),
                ("Appointments.Schedule", "Schedule appointments"),
                ("Appointments.View", "View appointments"),
                ("Appointments.Cancel", "Cancel appointments"),
                ("Appointments.Reschedule", "Reschedule appointments"),
                ("Appointments.AgendaView", "AgendaView appointments"),
                ("Appointments.AlertsView", "AlertsView appointments"),
                ("Appointments.SessionsManage", "SessionsManage appointments"),
                ("Appointments.AdminView", "AdminView appointments"),
            };

            foreach (var (code, name) in rows)
            {
                migrationBuilder.Sql($"""
                    INSERT INTO auth."Permissions" ("Id", "Code", "Name", "Module", "CreatedAt")
                    SELECT gen_random_uuid(), '{code}', '{name}', 'Appointments', now()
                    WHERE NOT EXISTS (
                        SELECT 1 FROM auth."Permissions" WHERE "Code" = '{code}'
                    );
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback: elimina SOLO los códigos nuevos sembrados por Up.
            migrationBuilder.Sql("""
                DELETE FROM auth."Permissions"
                WHERE "Code" IN (
                    'Appointments.RequestsCreate',
                    'Appointments.RequestsView',
                    'Appointments.RequestsConfirm',
                    'Appointments.Schedule',
                    'Appointments.View',
                    'Appointments.Cancel',
                    'Appointments.Reschedule',
                    'Appointments.AgendaView',
                    'Appointments.AlertsView',
                    'Appointments.SessionsManage',
                    'Appointments.AdminView'
                );
                """);
        }
    }
}
