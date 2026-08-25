using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Telemedicine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameTelemedicineAppointmentsToAppointments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Renombra SOLO la tabla de citas (zero-downtime: ALTER TABLE ... RENAME TO,
            // sin drop/recreate). El scaffold automático de EF produjo DropTable+CreateTable
            // (pérdida de datos) porque el snapshot seguía declarando la entidad
            // TelemedicineAppointment; esta migración se escribió a mano con renames.
            //
            // PostgreSQL reescribe automáticamente el predicado del índice único parcial
            // (ix_appointments_professional_start_active) y las referencias de las FK que
            // apuntan a esta tabla: pasan a mostrar "appointments" sin tocar datos.
            //
            // El índice único parcial creado por SQL crudo en AddAppointmentOverlapExclusion
            // (ix_appointments_request_id, anti doble confirmación) conserva su nombre; el
            // índice EF sobre request_id pasa a ix_appointments_request_id_lookup (nombre
            // explícito en AppointmentConfiguration) para no chocar con él.
            migrationBuilder.RenameTable(
                name: "telemedicine_appointments",
                schema: "tele",
                newName: "appointments");

            // PK: en PostgreSQL la constraint y su índice comparten nombre; renombrar el
            // índice renombra ambos (pk_telemedicine_appointments → pk_appointments).
            migrationBuilder.RenameIndex(
                name: "pk_telemedicine_appointments",
                schema: "tele",
                table: "appointments",
                newName: "pk_appointments");

            migrationBuilder.RenameIndex(
                name: "ix_telemedicine_appointments_organization_id_status",
                schema: "tele",
                table: "appointments",
                newName: "ix_appointments_organization_id_status");

            migrationBuilder.RenameIndex(
                name: "ix_telemedicine_appointments_patient_id",
                schema: "tele",
                table: "appointments",
                newName: "ix_appointments_patient_id");

            migrationBuilder.RenameIndex(
                name: "ix_telemedicine_appointments_professional_id",
                schema: "tele",
                table: "appointments",
                newName: "ix_appointments_professional_id");

            migrationBuilder.RenameIndex(
                name: "ix_telemedicine_appointments_request_id",
                schema: "tele",
                table: "appointments",
                newName: "ix_appointments_request_id_lookup");

            migrationBuilder.RenameIndex(
                name: "ix_telemedicine_appointments_scheduled_start",
                schema: "tele",
                table: "appointments",
                newName: "ix_appointments_scheduled_start");

            migrationBuilder.RenameIndex(
                name: "ix_telemedicine_appointments_specialty_id",
                schema: "tele",
                table: "appointments",
                newName: "ix_appointments_specialty_id");

            // FK de las tablas hijas hacia appointments: EF Core no expone una operación
            // RenameForeignKey (solo RenameTable/RenameIndex/RenameColumn/RenameSequence),
            // así que el rename de la constraint va por SQL crudo. ALTER TABLE ...
            // RENAME CONSTRAINT es metadata-only (sin reescritura de datos ni locks largos).
            migrationBuilder.Sql("""
                ALTER TABLE "tele"."appointment_cancellations"
                RENAME CONSTRAINT "fk_appointment_cancellations_telemedicine_appointments_appoint"
                TO "fk_appointment_cancellations_appointments_appointment_id";
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "tele"."appointment_reschedules"
                RENAME CONSTRAINT "fk_appointment_reschedules_telemedicine_appointments_appointme"
                TO "fk_appointment_reschedules_appointments_appointment_id";
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "tele"."clinical_encounters"
                RENAME CONSTRAINT "fk_clinical_encounters_telemedicine_appointments_appointment_id"
                TO "fk_clinical_encounters_appointments_appointment_id";
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "tele"."telemedicine_sessions"
                RENAME CONSTRAINT "fk_telemedicine_sessions_telemedicine_appointments_appointment"
                TO "fk_telemedicine_sessions_appointments_appointment_id";
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "tele"."virtual_rooms"
                RENAME CONSTRAINT "fk_virtual_rooms_telemedicine_appointments_appointment_id"
                TO "fk_virtual_rooms_appointments_appointment_id";
                """);

            // FK de appointments hacia telemedicine_requests (vive en la tabla renombrada).
            migrationBuilder.Sql("""
                ALTER TABLE "tele"."appointments"
                RENAME CONSTRAINT "fk_telemedicine_appointments_requests_request_id"
                TO "fk_appointments_requests_request_id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "tele"."appointments"
                RENAME CONSTRAINT "fk_appointments_requests_request_id"
                TO "fk_telemedicine_appointments_requests_request_id";
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "tele"."virtual_rooms"
                RENAME CONSTRAINT "fk_virtual_rooms_appointments_appointment_id"
                TO "fk_virtual_rooms_telemedicine_appointments_appointment_id";
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "tele"."telemedicine_sessions"
                RENAME CONSTRAINT "fk_telemedicine_sessions_appointments_appointment_id"
                TO "fk_telemedicine_sessions_telemedicine_appointments_appointment";
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "tele"."clinical_encounters"
                RENAME CONSTRAINT "fk_clinical_encounters_appointments_appointment_id"
                TO "fk_clinical_encounters_telemedicine_appointments_appointment_id";
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "tele"."appointment_reschedules"
                RENAME CONSTRAINT "fk_appointment_reschedules_appointments_appointment_id"
                TO "fk_appointment_reschedules_telemedicine_appointments_appointme";
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "tele"."appointment_cancellations"
                RENAME CONSTRAINT "fk_appointment_cancellations_appointments_appointment_id"
                TO "fk_appointment_cancellations_telemedicine_appointments_appoint";
                """);

            migrationBuilder.RenameIndex(
                name: "ix_appointments_specialty_id",
                schema: "tele",
                table: "telemedicine_appointments",
                newName: "ix_telemedicine_appointments_specialty_id");

            migrationBuilder.RenameIndex(
                name: "ix_appointments_scheduled_start",
                schema: "tele",
                table: "telemedicine_appointments",
                newName: "ix_telemedicine_appointments_scheduled_start");

            migrationBuilder.RenameIndex(
                name: "ix_appointments_request_id_lookup",
                schema: "tele",
                table: "telemedicine_appointments",
                newName: "ix_telemedicine_appointments_request_id");

            migrationBuilder.RenameIndex(
                name: "ix_appointments_professional_id",
                schema: "tele",
                table: "telemedicine_appointments",
                newName: "ix_telemedicine_appointments_professional_id");

            migrationBuilder.RenameIndex(
                name: "ix_appointments_patient_id",
                schema: "tele",
                table: "telemedicine_appointments",
                newName: "ix_telemedicine_appointments_patient_id");

            migrationBuilder.RenameIndex(
                name: "ix_appointments_organization_id_status",
                schema: "tele",
                table: "telemedicine_appointments",
                newName: "ix_telemedicine_appointments_organization_id_status");

            migrationBuilder.RenameIndex(
                name: "pk_appointments",
                schema: "tele",
                table: "telemedicine_appointments",
                newName: "pk_telemedicine_appointments");

            migrationBuilder.RenameTable(
                name: "appointments",
                schema: "tele",
                newName: "telemedicine_appointments");
        }
    }
}