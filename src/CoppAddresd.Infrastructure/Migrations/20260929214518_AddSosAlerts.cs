using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSosAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sos_alerts",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    accuracy_meters = table.Column<double>(type: "double precision", nullable: true),
                    location_captured_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    destination_phone_e164 = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sms_channel_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sms_updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    sms_detail = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    push_channel_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    push_updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    push_recipients = table.Column<int>(type: "integer", nullable: true),
                    push_detail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    attended_by = table.Column<Guid>(type: "uuid", nullable: true),
                    attended_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sos_alerts", x => x.id);
                    table.CheckConstraint("ck_sos_alerts_latitude_range", "\"latitude\" IS NULL OR (\"latitude\" >= -90.0 AND \"latitude\" <= 90.0)");
                    table.CheckConstraint("ck_sos_alerts_longitude_range", "\"longitude\" IS NULL OR (\"longitude\" >= -180.0 AND \"longitude\" <= 180.0)");
                    table.ForeignKey(
                        name: "FK_sos_alerts_patient_profiles_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sos_alerts_status_created_at",
                schema: "app",
                table: "sos_alerts",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "uq_sos_alerts_patient_active",
                schema: "app",
                table: "sos_alerts",
                column: "patient_id",
                unique: true,
                filter: "\"status\" = 'Activa'");

            migrationBuilder.CreateIndex(
                name: "uq_sos_alerts_patient_idempotency",
                schema: "app",
                table: "sos_alerts",
                columns: new[] { "patient_id", "idempotency_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sos_alerts",
                schema: "app");
        }
    }
}
