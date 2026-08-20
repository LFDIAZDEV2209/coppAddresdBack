using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Telemedicine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTelemedicineSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tele");

            migrationBuilder.CreateTable(
                name: "telemedicine_alerts",
                schema: "tele",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recipient_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    recipient_scope_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    severity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    related_appointment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_telemedicine_alerts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "telemedicine_requests",
                schema: "tele",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    professional_id = table.Column<Guid>(type: "uuid", nullable: true),
                    specialty_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    clinic_id = table.Column<Guid>(type: "uuid", nullable: true),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    preferred_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_telemedicine_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "telemedicine_settings",
                schema: "tele",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    clinic_id = table.Column<Guid>(type: "uuid", nullable: true),
                    default_appointment_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    min_advance_booking_hours = table.Column<int>(type: "integer", nullable: false),
                    max_advance_booking_days = table.Column<int>(type: "integer", nullable: false),
                    max_reschedules = table.Column<int>(type: "integer", nullable: false),
                    room_open_before_minutes = table.Column<int>(type: "integer", nullable: false),
                    room_close_after_minutes = table.Column<int>(type: "integer", nullable: false),
                    access_token_ttl_seconds = table.Column<int>(type: "integer", nullable: false),
                    max_participants = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_telemedicine_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "telemedicine_appointments",
                schema: "tele",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    professional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    specialty_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    clinic_id = table.Column<Guid>(type: "uuid", nullable: true),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    scheduled_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    scheduled_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reschedule_count = table.Column<int>(type: "integer", nullable: false),
                    cancellation_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    cancelled_by = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    no_show_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_telemedicine_appointments", x => x.id);
                    table.ForeignKey(
                        name: "fk_telemedicine_appointments_requests_request_id",
                        column: x => x.request_id,
                        principalSchema: "tele",
                        principalTable: "telemedicine_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "appointment_cancellations",
                schema: "tele",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cancelled_by = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    cancelled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_appointment_cancellations", x => x.id);
                    table.ForeignKey(
                        name: "fk_appointment_cancellations_telemedicine_appointments_appoint",
                        column: x => x.appointment_id,
                        principalSchema: "tele",
                        principalTable: "telemedicine_appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "appointment_reschedules",
                schema: "tele",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    from_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    to_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    rescheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_appointment_reschedules", x => x.id);
                    table.ForeignKey(
                        name: "fk_appointment_reschedules_telemedicine_appointments_appointme",
                        column: x => x.appointment_id,
                        principalSchema: "tele",
                        principalTable: "telemedicine_appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "virtual_rooms",
                schema: "tele",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    provider_room_sid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    provider_room_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    scheduled_open_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    scheduled_close_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    max_participants = table.Column<int>(type: "integer", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_virtual_rooms", x => x.id);
                    table.ForeignKey(
                        name: "fk_virtual_rooms_telemedicine_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalSchema: "tele",
                        principalTable: "telemedicine_appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "telemedicine_sessions",
                schema: "tele",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    duration_seconds = table.Column<long>(type: "bigint", nullable: true),
                    ended_by = table.Column<Guid>(type: "uuid", nullable: true),
                    end_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    last_provider_event_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_telemedicine_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_telemedicine_sessions_telemedicine_appointments_appointment",
                        column: x => x.appointment_id,
                        principalSchema: "tele",
                        principalTable: "telemedicine_appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_telemedicine_sessions_virtual_rooms_room_id",
                        column: x => x.room_id,
                        principalSchema: "tele",
                        principalTable: "virtual_rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "clinical_encounters",
                schema: "tele",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    professional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    encounter_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    clinical_data = table.Column<string>(type: "jsonb", nullable: true),
                    notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clinical_encounters", x => x.id);
                    table.ForeignKey(
                        name: "fk_clinical_encounters_sessions_session_id",
                        column: x => x.session_id,
                        principalSchema: "tele",
                        principalTable: "telemedicine_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_clinical_encounters_telemedicine_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalSchema: "tele",
                        principalTable: "telemedicine_appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_appointment_cancellations_appointment_id",
                schema: "tele",
                table: "appointment_cancellations",
                column: "appointment_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointment_reschedules_appointment_id",
                schema: "tele",
                table: "appointment_reschedules",
                column: "appointment_id");

            migrationBuilder.CreateIndex(
                name: "ix_clinical_encounters_appointment_id",
                schema: "tele",
                table: "clinical_encounters",
                column: "appointment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_clinical_encounters_encounter_date",
                schema: "tele",
                table: "clinical_encounters",
                column: "encounter_date");

            migrationBuilder.CreateIndex(
                name: "ix_clinical_encounters_patient_id",
                schema: "tele",
                table: "clinical_encounters",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_clinical_encounters_professional_id",
                schema: "tele",
                table: "clinical_encounters",
                column: "professional_id");

            migrationBuilder.CreateIndex(
                name: "ix_clinical_encounters_session_id",
                schema: "tele",
                table: "clinical_encounters",
                column: "session_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_telemedicine_alerts_created_at",
                schema: "tele",
                table: "telemedicine_alerts",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_telemedicine_alerts_recipient_type_recipient_scope_id",
                schema: "tele",
                table: "telemedicine_alerts",
                columns: new[] { "recipient_type", "recipient_scope_id" });

            migrationBuilder.CreateIndex(
                name: "ix_telemedicine_alerts_recipient_user_id_read_at",
                schema: "tele",
                table: "telemedicine_alerts",
                columns: new[] { "recipient_user_id", "read_at" });

            migrationBuilder.CreateIndex(
                name: "ix_telemedicine_alerts_related_appointment_id",
                schema: "tele",
                table: "telemedicine_alerts",
                column: "related_appointment_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_professional_start_active",
                schema: "tele",
                table: "telemedicine_appointments",
                columns: new[] { "professional_id", "scheduled_start" },
                unique: true,
                filter: "telemedicine_appointments.status IN ('Requested','Confirmed','InProgress')");

            migrationBuilder.CreateIndex(
                name: "ix_telemedicine_appointments_organization_id_status",
                schema: "tele",
                table: "telemedicine_appointments",
                columns: new[] { "organization_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_telemedicine_appointments_patient_id",
                schema: "tele",
                table: "telemedicine_appointments",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_telemedicine_appointments_professional_id",
                schema: "tele",
                table: "telemedicine_appointments",
                column: "professional_id");

            migrationBuilder.CreateIndex(
                name: "ix_telemedicine_appointments_request_id",
                schema: "tele",
                table: "telemedicine_appointments",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "ix_telemedicine_appointments_scheduled_start",
                schema: "tele",
                table: "telemedicine_appointments",
                column: "scheduled_start");

            migrationBuilder.CreateIndex(
                name: "ix_telemedicine_appointments_specialty_id",
                schema: "tele",
                table: "telemedicine_appointments",
                column: "specialty_id");

            migrationBuilder.CreateIndex(
                name: "ix_telemedicine_requests_organization_id_status",
                schema: "tele",
                table: "telemedicine_requests",
                columns: new[] { "organization_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_telemedicine_requests_patient_id",
                schema: "tele",
                table: "telemedicine_requests",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_telemedicine_requests_professional_id",
                schema: "tele",
                table: "telemedicine_requests",
                column: "professional_id");

            migrationBuilder.CreateIndex(
                name: "ix_telemedicine_requests_specialty_id",
                schema: "tele",
                table: "telemedicine_requests",
                column: "specialty_id");

            migrationBuilder.CreateIndex(
                name: "ix_telemedicine_requests_status",
                schema: "tele",
                table: "telemedicine_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_telemedicine_sessions_appointment_id",
                schema: "tele",
                table: "telemedicine_sessions",
                column: "appointment_id");

            migrationBuilder.CreateIndex(
                name: "ix_telemedicine_sessions_room_id",
                schema: "tele",
                table: "telemedicine_sessions",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "ix_telemedicine_sessions_status",
                schema: "tele",
                table: "telemedicine_sessions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_telemedicine_settings_organization_id_clinic_id",
                schema: "tele",
                table: "telemedicine_settings",
                columns: new[] { "organization_id", "clinic_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_virtual_rooms_appointment_id",
                schema: "tele",
                table: "virtual_rooms",
                column: "appointment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_virtual_rooms_provider_provider_room_name",
                schema: "tele",
                table: "virtual_rooms",
                columns: new[] { "provider", "provider_room_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_virtual_rooms_provider_room_sid",
                schema: "tele",
                table: "virtual_rooms",
                column: "provider_room_sid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "appointment_cancellations",
                schema: "tele");

            migrationBuilder.DropTable(
                name: "appointment_reschedules",
                schema: "tele");

            migrationBuilder.DropTable(
                name: "clinical_encounters",
                schema: "tele");

            migrationBuilder.DropTable(
                name: "telemedicine_alerts",
                schema: "tele");

            migrationBuilder.DropTable(
                name: "telemedicine_settings",
                schema: "tele");

            migrationBuilder.DropTable(
                name: "telemedicine_sessions",
                schema: "tele");

            migrationBuilder.DropTable(
                name: "virtual_rooms",
                schema: "tele");

            migrationBuilder.DropTable(
                name: "telemedicine_appointments",
                schema: "tele");

            migrationBuilder.DropTable(
                name: "telemedicine_requests",
                schema: "tele");
        }
    }
}
