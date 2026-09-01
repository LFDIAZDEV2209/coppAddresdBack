using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHealthTestsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "health_test_alert_rules",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    condition = table.Column<string>(type: "jsonb", nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "high"),
                    message_template = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_test_alert_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "health_test_batteries",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    auto_assign_on_patient_create = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_test_batteries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "health_test_indicator_defs",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    computation = table.Column<string>(type: "jsonb", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_test_indicator_defs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "health_test_instruments",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_test_instruments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "health_test_battery_assignments",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    battery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    assigned_by = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    due_date = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_test_battery_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_health_test_battery_assignments_health_test_batteries_batte~",
                        column: x => x.battery_id,
                        principalSchema: "app",
                        principalTable: "health_test_batteries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_health_test_battery_assignments_patient_profiles_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "health_test_versions",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    instrument_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "draft"),
                    is_current = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    scoring_strategy = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "sum"),
                    points = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    published_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    retired_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_test_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_health_test_versions_health_test_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalSchema: "app",
                        principalTable: "health_test_instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "health_test_assignments",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    battery_assignment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    priority = table.Column<int>(type: "integer", nullable: true),
                    assigned_by = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    started_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    due_date = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_test_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_health_test_assignments_health_test_battery_assignments_bat~",
                        column: x => x.battery_assignment_id,
                        principalSchema: "app",
                        principalTable: "health_test_battery_assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_health_test_assignments_health_test_versions_version_id",
                        column: x => x.version_id,
                        principalSchema: "app",
                        principalTable: "health_test_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_health_test_assignments_patient_profiles_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "health_test_battery_items",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    battery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instrument_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    frequency_days = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_test_battery_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_health_test_battery_items_health_test_batteries_battery_id",
                        column: x => x.battery_id,
                        principalSchema: "app",
                        principalTable: "health_test_batteries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_health_test_battery_items_health_test_instruments_instrumen~",
                        column: x => x.instrument_id,
                        principalSchema: "app",
                        principalTable: "health_test_instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_health_test_battery_items_health_test_versions_version_id",
                        column: x => x.version_id,
                        principalSchema: "app",
                        principalTable: "health_test_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "health_test_questions",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    section = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    text = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "scale"),
                    scoring_direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "positive"),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_test_questions", x => x.id);
                    table.ForeignKey(
                        name: "FK_health_test_questions_health_test_versions_version_id",
                        column: x => x.version_id,
                        principalSchema: "app",
                        principalTable: "health_test_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "health_test_score_ranges",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    min_value = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    max_value = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "low"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_test_score_ranges", x => x.id);
                    table.ForeignKey(
                        name: "FK_health_test_score_ranges_health_test_versions_version_id",
                        column: x => x.version_id,
                        principalSchema: "app",
                        principalTable: "health_test_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "health_test_evaluations",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "started"),
                    started_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    completed_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    score = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    score_percentage = table.Column<decimal>(type: "numeric(6,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_test_evaluations", x => x.id);
                    table.ForeignKey(
                        name: "FK_health_test_evaluations_health_test_assignments_assignment_~",
                        column: x => x.assignment_id,
                        principalSchema: "app",
                        principalTable: "health_test_assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_health_test_evaluations_health_test_versions_version_id",
                        column: x => x.version_id,
                        principalSchema: "app",
                        principalTable: "health_test_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_health_test_evaluations_patient_profiles_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "health_test_answer_options",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    score_value = table.Column<decimal>(type: "numeric(8,2)", nullable: true),
                    depends_on_question_id = table.Column<Guid>(type: "uuid", nullable: true),
                    depends_on_option_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_test_answer_options", x => x.id);
                    table.ForeignKey(
                        name: "FK_health_test_answer_options_health_test_questions_question_id",
                        column: x => x.question_id,
                        principalSchema: "app",
                        principalTable: "health_test_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "health_test_comments",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_test_comments", x => x.id);
                    table.ForeignKey(
                        name: "FK_health_test_comments_health_test_evaluations_evaluation_id",
                        column: x => x.evaluation_id,
                        principalSchema: "app",
                        principalTable: "health_test_evaluations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_health_test_comments_patient_profiles_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "health_test_results",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    evaluation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    result_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    value = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    qualifier = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_test_results", x => x.id);
                    table.ForeignKey(
                        name: "FK_health_test_results_health_test_evaluations_evaluation_id",
                        column: x => x.evaluation_id,
                        principalSchema: "app",
                        principalTable: "health_test_evaluations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "health_test_responses",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    evaluation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    answer_option_id = table.Column<Guid>(type: "uuid", nullable: true),
                    value_text = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_test_responses", x => x.id);
                    table.ForeignKey(
                        name: "FK_health_test_responses_health_test_answer_options_answer_opt~",
                        column: x => x.answer_option_id,
                        principalSchema: "app",
                        principalTable: "health_test_answer_options",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_health_test_responses_health_test_evaluations_evaluation_id",
                        column: x => x.evaluation_id,
                        principalSchema: "app",
                        principalTable: "health_test_evaluations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_health_test_responses_health_test_questions_question_id",
                        column: x => x.question_id,
                        principalSchema: "app",
                        principalTable: "health_test_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "health_test_alerts",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    result_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "high"),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    body = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "active"),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    resolved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_test_alerts", x => x.id);
                    table.ForeignKey(
                        name: "FK_health_test_alerts_health_test_alert_rules_rule_id",
                        column: x => x.rule_id,
                        principalSchema: "app",
                        principalTable: "health_test_alert_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_health_test_alerts_health_test_results_result_id",
                        column: x => x.result_id,
                        principalSchema: "app",
                        principalTable: "health_test_results",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_health_test_alerts_patient_profiles_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_health_test_alert_rules_code",
                schema: "app",
                table: "health_test_alert_rules",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_health_test_alert_rules_is_active",
                schema: "app",
                table: "health_test_alert_rules",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_health_test_alerts_patient_id",
                schema: "app",
                table: "health_test_alerts",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_health_test_alerts_result_rule",
                schema: "app",
                table: "health_test_alerts",
                columns: new[] { "result_id", "rule_id" });

            migrationBuilder.CreateIndex(
                name: "IX_health_test_alerts_rule_id",
                schema: "app",
                table: "health_test_alerts",
                column: "rule_id");

            migrationBuilder.CreateIndex(
                name: "ix_health_test_alerts_status_severity",
                schema: "app",
                table: "health_test_alerts",
                columns: new[] { "status", "severity" });

            migrationBuilder.CreateIndex(
                name: "ix_health_test_answer_options_question_id",
                schema: "app",
                table: "health_test_answer_options",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_health_test_assignments_battery_assignment_id",
                schema: "app",
                table: "health_test_assignments",
                column: "battery_assignment_id");

            migrationBuilder.CreateIndex(
                name: "ix_health_test_assignments_patient_id",
                schema: "app",
                table: "health_test_assignments",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_health_test_assignments_patient_status",
                schema: "app",
                table: "health_test_assignments",
                columns: new[] { "patient_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_health_test_assignments_status_due",
                schema: "app",
                table: "health_test_assignments",
                columns: new[] { "status", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ix_health_test_assignments_version_id",
                schema: "app",
                table: "health_test_assignments",
                column: "version_id");

            migrationBuilder.CreateIndex(
                name: "ix_health_test_batteries_code",
                schema: "app",
                table: "health_test_batteries",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_health_test_batteries_is_active",
                schema: "app",
                table: "health_test_batteries",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_health_test_battery_assignments_battery_id",
                schema: "app",
                table: "health_test_battery_assignments",
                column: "battery_id");

            migrationBuilder.CreateIndex(
                name: "ix_health_test_battery_assignments_patient_id",
                schema: "app",
                table: "health_test_battery_assignments",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_health_test_battery_assignments_patient_status",
                schema: "app",
                table: "health_test_battery_assignments",
                columns: new[] { "patient_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_health_test_battery_items_battery_id",
                schema: "app",
                table: "health_test_battery_items",
                column: "battery_id");

            migrationBuilder.CreateIndex(
                name: "ix_health_test_battery_items_battery_instrument",
                schema: "app",
                table: "health_test_battery_items",
                columns: new[] { "battery_id", "instrument_id" });

            migrationBuilder.CreateIndex(
                name: "ix_health_test_battery_items_instrument_id",
                schema: "app",
                table: "health_test_battery_items",
                column: "instrument_id");

            migrationBuilder.CreateIndex(
                name: "IX_health_test_battery_items_version_id",
                schema: "app",
                table: "health_test_battery_items",
                column: "version_id");

            migrationBuilder.CreateIndex(
                name: "ix_health_test_comments_evaluation_id",
                schema: "app",
                table: "health_test_comments",
                column: "evaluation_id");

            migrationBuilder.CreateIndex(
                name: "ix_health_test_comments_patient_id",
                schema: "app",
                table: "health_test_comments",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_health_test_evaluations_assignment_id",
                schema: "app",
                table: "health_test_evaluations",
                column: "assignment_id");

            migrationBuilder.CreateIndex(
                name: "ix_health_test_evaluations_patient_completed",
                schema: "app",
                table: "health_test_evaluations",
                columns: new[] { "patient_id", "completed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_health_test_evaluations_patient_status",
                schema: "app",
                table: "health_test_evaluations",
                columns: new[] { "patient_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_health_test_evaluations_version_id",
                schema: "app",
                table: "health_test_evaluations",
                column: "version_id");

            migrationBuilder.CreateIndex(
                name: "ix_health_test_indicator_defs_code",
                schema: "app",
                table: "health_test_indicator_defs",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_health_test_indicator_defs_is_active",
                schema: "app",
                table: "health_test_indicator_defs",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_health_test_instruments_category",
                schema: "app",
                table: "health_test_instruments",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "ix_health_test_instruments_code",
                schema: "app",
                table: "health_test_instruments",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_health_test_instruments_is_active",
                schema: "app",
                table: "health_test_instruments",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_health_test_questions_version_code",
                schema: "app",
                table: "health_test_questions",
                columns: new[] { "version_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_health_test_questions_version_id",
                schema: "app",
                table: "health_test_questions",
                column: "version_id");

            migrationBuilder.CreateIndex(
                name: "IX_health_test_responses_answer_option_id",
                schema: "app",
                table: "health_test_responses",
                column: "answer_option_id");

            migrationBuilder.CreateIndex(
                name: "ix_health_test_responses_evaluation_id",
                schema: "app",
                table: "health_test_responses",
                column: "evaluation_id");

            migrationBuilder.CreateIndex(
                name: "ix_health_test_responses_evaluation_question",
                schema: "app",
                table: "health_test_responses",
                columns: new[] { "evaluation_id", "question_id" });

            migrationBuilder.CreateIndex(
                name: "IX_health_test_responses_question_id",
                schema: "app",
                table: "health_test_responses",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_health_test_results_evaluation_code",
                schema: "app",
                table: "health_test_results",
                columns: new[] { "evaluation_id", "code" });

            migrationBuilder.CreateIndex(
                name: "ix_health_test_results_evaluation_id",
                schema: "app",
                table: "health_test_results",
                column: "evaluation_id");

            migrationBuilder.CreateIndex(
                name: "ix_health_test_score_ranges_version_id",
                schema: "app",
                table: "health_test_score_ranges",
                column: "version_id");

            migrationBuilder.CreateIndex(
                name: "ix_health_test_versions_instrument_current",
                schema: "app",
                table: "health_test_versions",
                columns: new[] { "instrument_id", "is_current" });

            migrationBuilder.CreateIndex(
                name: "ix_health_test_versions_instrument_id",
                schema: "app",
                table: "health_test_versions",
                column: "instrument_id");

            migrationBuilder.CreateIndex(
                name: "ix_health_test_versions_instrument_number",
                schema: "app",
                table: "health_test_versions",
                columns: new[] { "instrument_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_health_test_versions_status",
                schema: "app",
                table: "health_test_versions",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "health_test_alerts",
                schema: "app");

            migrationBuilder.DropTable(
                name: "health_test_battery_items",
                schema: "app");

            migrationBuilder.DropTable(
                name: "health_test_comments",
                schema: "app");

            migrationBuilder.DropTable(
                name: "health_test_indicator_defs",
                schema: "app");

            migrationBuilder.DropTable(
                name: "health_test_responses",
                schema: "app");

            migrationBuilder.DropTable(
                name: "health_test_score_ranges",
                schema: "app");

            migrationBuilder.DropTable(
                name: "health_test_alert_rules",
                schema: "app");

            migrationBuilder.DropTable(
                name: "health_test_results",
                schema: "app");

            migrationBuilder.DropTable(
                name: "health_test_answer_options",
                schema: "app");

            migrationBuilder.DropTable(
                name: "health_test_evaluations",
                schema: "app");

            migrationBuilder.DropTable(
                name: "health_test_questions",
                schema: "app");

            migrationBuilder.DropTable(
                name: "health_test_assignments",
                schema: "app");

            migrationBuilder.DropTable(
                name: "health_test_battery_assignments",
                schema: "app");

            migrationBuilder.DropTable(
                name: "health_test_versions",
                schema: "app");

            migrationBuilder.DropTable(
                name: "health_test_batteries",
                schema: "app");

            migrationBuilder.DropTable(
                name: "health_test_instruments",
                schema: "app");
        }
    }
}
