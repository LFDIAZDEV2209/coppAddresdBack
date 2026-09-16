using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHealthTestAlertNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "health_test_notification_templates",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "community"),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    test_category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    indicator_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    body_template = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_test_notification_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "health_test_notification_template_versions",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    test_category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    indicator_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    body_template = table.Column<string>(type: "text", nullable: false),
                    note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_test_notification_template_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_health_test_notification_template_versions_health_test_noti~",
                        column: x => x.template_id,
                        principalSchema: "app",
                        principalTable: "health_test_notification_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "health_test_notifications",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    alert_id = table.Column<Guid>(type: "uuid", nullable: true),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: true),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "community"),
                    template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recipient = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    rendered_body = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "queued"),
                    provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    provider_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    error = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    sent_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_test_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_health_test_notifications_health_test_alerts_alert_id",
                        column: x => x.alert_id,
                        principalSchema: "app",
                        principalTable: "health_test_alerts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_health_test_notifications_health_test_notification_template~",
                        column: x => x.template_id,
                        principalSchema: "app",
                        principalTable: "health_test_notification_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_health_test_notifications_patient_profiles_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_health_test_notification_template_versions_template_version",
                schema: "app",
                table: "health_test_notification_template_versions",
                columns: new[] { "template_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_health_test_notification_templates_channel",
                schema: "app",
                table: "health_test_notification_templates",
                column: "channel");

            migrationBuilder.CreateIndex(
                name: "ix_health_test_notification_templates_code",
                schema: "app",
                table: "health_test_notification_templates",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_health_test_notification_templates_is_active",
                schema: "app",
                table: "health_test_notification_templates",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_health_test_notifications_alert_id",
                schema: "app",
                table: "health_test_notifications",
                column: "alert_id");

            migrationBuilder.CreateIndex(
                name: "ix_health_test_notifications_channel",
                schema: "app",
                table: "health_test_notifications",
                column: "channel");

            migrationBuilder.CreateIndex(
                name: "ix_health_test_notifications_patient_id",
                schema: "app",
                table: "health_test_notifications",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_health_test_notifications_status_created_at",
                schema: "app",
                table: "health_test_notifications",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_health_test_notifications_template_id",
                schema: "app",
                table: "health_test_notifications",
                column: "template_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "health_test_notification_template_versions",
                schema: "app");

            migrationBuilder.DropTable(
                name: "health_test_notifications",
                schema: "app");

            migrationBuilder.DropTable(
                name: "health_test_notification_templates",
                schema: "app");
        }
    }
}
