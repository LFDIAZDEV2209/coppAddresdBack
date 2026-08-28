using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramProgressCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "program_templates",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    total_weeks = table.Column<int>(type: "integer", nullable: false, defaultValue: 83),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Draft"),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    published_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_templates", x => x.id);
                    table.CheckConstraint("ck_program_templates_total_weeks_positive", "\"total_weeks\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "program_enrollments",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    timezone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "America/Bogota"),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    started_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    start_local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    current_week_number = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    completed_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    paused_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    withdrawn_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_enrollments", x => x.id);
                    table.CheckConstraint("ck_program_enrollments_current_week_positive", "\"current_week_number\" >= 1");
                    table.ForeignKey(
                        name: "FK_program_enrollments_patient_profiles_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_program_enrollments_program_templates_template_id",
                        column: x => x.template_id,
                        principalSchema: "app",
                        principalTable: "program_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "weekly_day_templates",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    weekday = table.Column<short>(type: "smallint", nullable: false),
                    task_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weekly_day_templates", x => x.id);
                    table.CheckConstraint("ck_weekly_day_templates_points_non_negative", "\"points\" >= 0");
                    table.CheckConstraint("ck_weekly_day_templates_weekday_range", "\"weekday\" BETWEEN 1 AND 7");
                    table.ForeignKey(
                        name: "FK_weekly_day_templates_program_templates_template_id",
                        column: x => x.template_id,
                        principalSchema: "app",
                        principalTable: "program_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "adaptation_recommendations",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    target_entity_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    target_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payload = table.Column<JsonElement>(type: "jsonb", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    requires_approval = table.Column<bool>(type: "boolean", nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    applied_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_adaptation_recommendations", x => x.id);
                    table.ForeignKey(
                        name: "FK_adaptation_recommendations_program_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalSchema: "app",
                        principalTable: "program_enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "emotional_records",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    program_enrollment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recorded_local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    mood_score = table.Column<short>(type: "smallint", nullable: false),
                    barriers = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_emotional_records", x => x.id);
                    table.CheckConstraint("ck_emotional_records_mood_score_range", "\"mood_score\" BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_emotional_records_patient_profiles_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_emotional_records_program_enrollments_program_enrollment_id",
                        column: x => x.program_enrollment_id,
                        principalSchema: "app",
                        principalTable: "program_enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "program_weeks",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    week_number = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Locked"),
                    week_start_date_local = table.Column<DateOnly>(type: "date", nullable: false),
                    week_end_date_local = table.Column<DateOnly>(type: "date", nullable: false),
                    tasks_snapshot = table.Column<JsonElement>(type: "jsonb", nullable: false),
                    template_version_at_start = table.Column<int>(type: "integer", nullable: false),
                    activated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_weeks", x => x.id);
                    table.CheckConstraint("ck_program_weeks_week_number_positive", "\"week_number\" >= 1");
                    table.ForeignKey(
                        name: "FK_program_weeks_program_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalSchema: "app",
                        principalTable: "program_enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "streak_freezes",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    used_on_local_date = table.Column<DateOnly>(type: "date", nullable: true),
                    granted_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    granted_reason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_streak_freezes", x => x.id);
                    table.ForeignKey(
                        name: "FK_streak_freezes_program_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalSchema: "app",
                        principalTable: "program_enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "streak_states",
                schema: "app",
                columns: table => new
                {
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_streak = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    longest_streak = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_active_date = table.Column<DateOnly>(type: "date", nullable: true),
                    freezes_remaining = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    freezes_used_total = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_break_date = table.Column<DateOnly>(type: "date", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_streak_states", x => x.enrollment_id);
                    table.CheckConstraint("ck_streak_states_freezes_remaining_range", "\"freezes_remaining\" >= 0 AND \"freezes_remaining\" <= 3");
                    table.ForeignKey(
                        name: "FK_streak_states_program_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalSchema: "app",
                        principalTable: "program_enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "xp_ledger",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    source_ref_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    source_ref_id = table.Column<Guid>(type: "uuid", nullable: true),
                    balance_after = table.Column<int>(type: "integer", nullable: false),
                    awarded_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    granted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xp_ledger", x => x.id);
                    table.ForeignKey(
                        name: "FK_xp_ledger_program_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalSchema: "app",
                        principalTable: "program_enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "daily_checkins",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    program_week_id = table.Column<Guid>(type: "uuid", nullable: false),
                    local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    weekday = table.Column<short>(type: "smallint", nullable: false),
                    mood_score = table.Column<short>(type: "smallint", nullable: true),
                    barriers = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    total_points = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    bonus_awarded = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_perfect_day = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_checkins", x => x.id);
                    table.CheckConstraint("ck_daily_checkins_mood_score_range", "\"mood_score\" BETWEEN 1 AND 5");
                    table.CheckConstraint("ck_daily_checkins_weekday_range", "\"weekday\" BETWEEN 1 AND 7");
                    table.ForeignKey(
                        name: "FK_daily_checkins_program_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalSchema: "app",
                        principalTable: "program_enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_daily_checkins_program_weeks_program_week_id",
                        column: x => x.program_week_id,
                        principalSchema: "app",
                        principalTable: "program_weeks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_completions",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    program_week_id = table.Column<Guid>(type: "uuid", nullable: false),
                    daily_checkin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    weekday = table.Column<short>(type: "smallint", nullable: false),
                    task_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    points_awarded = table.Column<int>(type: "integer", nullable: false),
                    client_request_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    client_completed_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    source_ref_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    nutrition_plan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nutrition_plan_day_number = table.Column<short>(type: "smallint", nullable: true),
                    exercise_routine_id = table.Column<Guid>(type: "uuid", nullable: true),
                    media_id = table.Column<Guid>(type: "uuid", nullable: true),
                    vital_signs_batch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nutribiotic_product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    emotional_record_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_completions", x => x.id);
                    table.CheckConstraint("ck_task_completions_plan_day_number_range", "\"nutrition_plan_day_number\" BETWEEN 1 AND 7");
                    table.CheckConstraint("ck_task_completions_weekday_range", "\"weekday\" BETWEEN 1 AND 7");
                    table.ForeignKey(
                        name: "FK_task_completions_daily_checkins_daily_checkin_id",
                        column: x => x.daily_checkin_id,
                        principalSchema: "app",
                        principalTable: "daily_checkins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_task_completions_emotional_records_emotional_record_id",
                        column: x => x.emotional_record_id,
                        principalSchema: "app",
                        principalTable: "emotional_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_completions_exercise_routines_exercise_routine_id",
                        column: x => x.exercise_routine_id,
                        principalSchema: "app",
                        principalTable: "exercise_routines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_completions_media_items_media_id",
                        column: x => x.media_id,
                        principalSchema: "app",
                        principalTable: "media_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_completions_nutrition_plans_nutrition_plan_id",
                        column: x => x.nutrition_plan_id,
                        principalSchema: "app",
                        principalTable: "nutrition_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_completions_products_nutribiotic_product_id",
                        column: x => x.nutribiotic_product_id,
                        principalSchema: "erp",
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_completions_program_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalSchema: "app",
                        principalTable: "program_enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_task_completions_program_weeks_program_week_id",
                        column: x => x.program_week_id,
                        principalSchema: "app",
                        principalTable: "program_weeks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_task_completions_vital_signs_vital_signs_batch_id",
                        column: x => x.vital_signs_batch_id,
                        principalSchema: "app",
                        principalTable: "vital_signs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_adaptation_recommendations_enrollment_id",
                schema: "app",
                table: "adaptation_recommendations",
                column: "enrollment_id");

            migrationBuilder.CreateIndex(
                name: "ix_adaptation_recommendations_kind",
                schema: "app",
                table: "adaptation_recommendations",
                column: "kind");

            migrationBuilder.CreateIndex(
                name: "ix_adaptation_recommendations_status",
                schema: "app",
                table: "adaptation_recommendations",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_daily_checkins_enrollment_id",
                schema: "app",
                table: "daily_checkins",
                column: "enrollment_id");

            migrationBuilder.CreateIndex(
                name: "ix_daily_checkins_program_week_id",
                schema: "app",
                table: "daily_checkins",
                column: "program_week_id");

            migrationBuilder.CreateIndex(
                name: "uq_daily_checkins_enrollment_date",
                schema: "app",
                table: "daily_checkins",
                columns: new[] { "enrollment_id", "local_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_emotional_records_patient_id",
                schema: "app",
                table: "emotional_records",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_emotional_records_program_enrollment_id",
                schema: "app",
                table: "emotional_records",
                column: "program_enrollment_id");

            migrationBuilder.CreateIndex(
                name: "uq_emotional_records_enrollment_date",
                schema: "app",
                table: "emotional_records",
                columns: new[] { "program_enrollment_id", "recorded_local_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_program_enrollments_status",
                schema: "app",
                table: "program_enrollments",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_program_enrollments_template_id",
                schema: "app",
                table: "program_enrollments",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "uq_program_enrollments_patient_active",
                schema: "app",
                table: "program_enrollments",
                column: "patient_id",
                unique: true,
                filter: "\"status\" = 'Active'");

            // SPEC §3.3: lookup por patient_id en cualquier estado. EF deduplica
            // índices por set de columnas, por lo que el índice simple se crea
            // aquí por SQL (fuera del modelo: invisible para futuros diffs).
            migrationBuilder.CreateIndex(
                name: "ix_program_enrollments_patient_id",
                schema: "app",
                table: "program_enrollments",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_program_templates_code",
                schema: "app",
                table: "program_templates",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_program_templates_status",
                schema: "app",
                table: "program_templates",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_program_weeks_enrollment_id",
                schema: "app",
                table: "program_weeks",
                column: "enrollment_id");

            migrationBuilder.CreateIndex(
                name: "ix_program_weeks_status",
                schema: "app",
                table: "program_weeks",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_program_weeks_week_start_date_local",
                schema: "app",
                table: "program_weeks",
                column: "week_start_date_local");

            migrationBuilder.CreateIndex(
                name: "uq_program_weeks_enrollment_week",
                schema: "app",
                table: "program_weeks",
                columns: new[] { "enrollment_id", "week_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_streak_freezes_enrollment_id",
                schema: "app",
                table: "streak_freezes",
                column: "enrollment_id");

            migrationBuilder.CreateIndex(
                name: "ix_streak_freezes_kind",
                schema: "app",
                table: "streak_freezes",
                column: "kind");

            migrationBuilder.CreateIndex(
                name: "ix_streak_states_last_active_date",
                schema: "app",
                table: "streak_states",
                column: "last_active_date");

            migrationBuilder.CreateIndex(
                name: "ix_task_completions_client_request_id",
                schema: "app",
                table: "task_completions",
                column: "client_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_completions_daily_checkin_id",
                schema: "app",
                table: "task_completions",
                column: "daily_checkin_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_completions_emotional_record_id",
                schema: "app",
                table: "task_completions",
                column: "emotional_record_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_completions_enrollment_id",
                schema: "app",
                table: "task_completions",
                column: "enrollment_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_completions_exercise_routine_id",
                schema: "app",
                table: "task_completions",
                column: "exercise_routine_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_completions_media_id",
                schema: "app",
                table: "task_completions",
                column: "media_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_completions_nutribiotic_product_id",
                schema: "app",
                table: "task_completions",
                column: "nutribiotic_product_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_completions_nutrition_plan_id",
                schema: "app",
                table: "task_completions",
                column: "nutrition_plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_completions_program_week_id",
                schema: "app",
                table: "task_completions",
                column: "program_week_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_completions_vital_signs_batch_id",
                schema: "app",
                table: "task_completions",
                column: "vital_signs_batch_id");

            migrationBuilder.CreateIndex(
                name: "uq_task_completions_enrollment_date_task",
                schema: "app",
                table: "task_completions",
                columns: new[] { "enrollment_id", "local_date", "task_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_weekly_day_templates_template_id",
                schema: "app",
                table: "weekly_day_templates",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "uq_weekly_day_templates_template_weekday_task",
                schema: "app",
                table: "weekly_day_templates",
                columns: new[] { "template_id", "weekday", "task_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_xp_ledger_enrollment_awarded_at",
                schema: "app",
                table: "xp_ledger",
                columns: new[] { "enrollment_id", "awarded_at" });

            migrationBuilder.CreateIndex(
                name: "ix_xp_ledger_enrollment_id",
                schema: "app",
                table: "xp_ledger",
                column: "enrollment_id");

            migrationBuilder.CreateIndex(
                name: "uq_xp_ledger_source_dedupe",
                schema: "app",
                table: "xp_ledger",
                columns: new[] { "source_ref_type", "source_ref_id", "reason" },
                unique: true,
                filter: "\"source_ref_id\" IS NOT NULL");

            // FKs hacia auth.users (creadas por SQL, patrón user_id): la tabla
            // "Users" vive en el schema auth y la gestiona el Auth Service
            // (otro DbContext). Requiere que las migraciones del Auth Service
            // ya se hayan aplicado. SET NULL preserva la auditoría si se
            // elimina un usuario.
            migrationBuilder.Sql("""
                ALTER TABLE app.program_templates
                    ADD CONSTRAINT fk_program_templates_created_by
                    FOREIGN KEY (created_by) REFERENCES auth."Users" ("Id")
                    ON DELETE SET NULL;

                ALTER TABLE app.program_templates
                    ADD CONSTRAINT fk_program_templates_updated_by
                    FOREIGN KEY (updated_by) REFERENCES auth."Users" ("Id")
                    ON DELETE SET NULL;

                ALTER TABLE app.weekly_day_templates
                    ADD CONSTRAINT fk_weekly_day_templates_created_by
                    FOREIGN KEY (created_by) REFERENCES auth."Users" ("Id")
                    ON DELETE SET NULL;

                ALTER TABLE app.program_enrollments
                    ADD CONSTRAINT fk_program_enrollments_created_by
                    FOREIGN KEY (created_by) REFERENCES auth."Users" ("Id")
                    ON DELETE SET NULL;

                ALTER TABLE app.program_enrollments
                    ADD CONSTRAINT fk_program_enrollments_updated_by
                    FOREIGN KEY (updated_by) REFERENCES auth."Users" ("Id")
                    ON DELETE SET NULL;

                ALTER TABLE app.xp_ledger
                    ADD CONSTRAINT fk_xp_ledger_granted_by
                    FOREIGN KEY (granted_by) REFERENCES auth."Users" ("Id")
                    ON DELETE SET NULL;

                ALTER TABLE app.adaptation_recommendations
                    ADD CONSTRAINT fk_adaptation_recommendations_requested_by
                    FOREIGN KEY (requested_by) REFERENCES auth."Users" ("Id")
                    ON DELETE SET NULL;

                ALTER TABLE app.adaptation_recommendations
                    ADD CONSTRAINT fk_adaptation_recommendations_decided_by
                    FOREIGN KEY (decided_by) REFERENCES auth."Users" ("Id")
                    ON DELETE SET NULL;
                """);

            // Permisos mínimos para el rol de la aplicación (convención app_user).
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON app.program_templates TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON app.weekly_day_templates TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON app.program_enrollments TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON app.program_weeks TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON app.daily_checkins TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON app.task_completions TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON app.xp_ledger TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON app.streak_states TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON app.streak_freezes TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON app.adaptation_recommendations TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON app.emotional_records TO app_user;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE app.program_templates DROP CONSTRAINT IF EXISTS fk_program_templates_created_by;
                ALTER TABLE app.program_templates DROP CONSTRAINT IF EXISTS fk_program_templates_updated_by;
                ALTER TABLE app.weekly_day_templates DROP CONSTRAINT IF EXISTS fk_weekly_day_templates_created_by;
                ALTER TABLE app.program_enrollments DROP CONSTRAINT IF EXISTS fk_program_enrollments_created_by;
                ALTER TABLE app.program_enrollments DROP CONSTRAINT IF EXISTS fk_program_enrollments_updated_by;
                ALTER TABLE app.xp_ledger DROP CONSTRAINT IF EXISTS fk_xp_ledger_granted_by;
                ALTER TABLE app.adaptation_recommendations DROP CONSTRAINT IF EXISTS fk_adaptation_recommendations_requested_by;
                ALTER TABLE app.adaptation_recommendations DROP CONSTRAINT IF EXISTS fk_adaptation_recommendations_decided_by;
                """);

            migrationBuilder.DropTable(
                name: "adaptation_recommendations",
                schema: "app");

            migrationBuilder.DropTable(
                name: "streak_freezes",
                schema: "app");

            migrationBuilder.DropTable(
                name: "streak_states",
                schema: "app");

            migrationBuilder.DropTable(
                name: "task_completions",
                schema: "app");

            migrationBuilder.DropTable(
                name: "weekly_day_templates",
                schema: "app");

            migrationBuilder.DropTable(
                name: "xp_ledger",
                schema: "app");

            migrationBuilder.DropTable(
                name: "daily_checkins",
                schema: "app");

            migrationBuilder.DropTable(
                name: "emotional_records",
                schema: "app");

            migrationBuilder.DropTable(
                name: "program_weeks",
                schema: "app");

            migrationBuilder.DropTable(
                name: "program_enrollments",
                schema: "app");

            migrationBuilder.DropTable(
                name: "program_templates",
                schema: "app");
        }
    }
}
