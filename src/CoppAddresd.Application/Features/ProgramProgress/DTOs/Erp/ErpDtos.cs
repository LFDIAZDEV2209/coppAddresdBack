using System.Text.Json.Serialization;

namespace CoppAddresd.Application.Features.ProgramProgress.DTOs.Erp;

// ===================== Dashboard =====================

public sealed record ErpDashboardKpis(
    [property: JsonPropertyName("adherence_global_hoy")] decimal AdherenceGlobalHoy,
    [property: JsonPropertyName("xp_semana")] int XpSemana,
    [property: JsonPropertyName("pacientes_racha_gt7")] int PacientesRachaGt7,
    [property: JsonPropertyName("en_riesgo")] int EnRiesgo,
    [property: JsonPropertyName("total_active")] int TotalActive,
    [property: JsonPropertyName("bmi_promedio")] decimal? BmiPromedio,
    [property: JsonPropertyName("hba1c_promedio")] decimal? Hba1cPromedio,
    [property: JsonPropertyName("body_fat_promedio")] decimal? BodyFatPromedio,
    [property: JsonPropertyName("pacientes_bmi_ge30")] int PacientesBmiGe30,
    [property: JsonPropertyName("pacientes_hba1c_ge7")] int PacientesHba1cGe7,
    [property: JsonPropertyName("pacientes_body_fat_alto")] int PacientesBodyFatAlto);

public sealed record ErpMissionAdherence(
    [property: JsonPropertyName("task_code")] string TaskCode,
    [property: JsonPropertyName("completed")] int Completed,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("pct")] decimal Pct);

public sealed record ErpStreakBucket(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("count")] int Count);

public sealed record ErpXpByCategory(
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("total")] int Total);

public sealed record ErpDailyAdherence(
    [property: JsonPropertyName("date")] DateOnly Date,
    [property: JsonPropertyName("pct")] decimal Pct);

public sealed record ErpLeaderboardEntry(
    [property: JsonPropertyName("rank")] int Rank,
    [property: JsonPropertyName("patient_id")] Guid PatientId,
    [property: JsonPropertyName("patient_name")] string PatientName,
    [property: JsonPropertyName("enrollment_id")] Guid EnrollmentId,
    [property: JsonPropertyName("xp")] int Xp,
    [property: JsonPropertyName("adherence")] decimal Adherence,
    [property: JsonPropertyName("current_streak")] int CurrentStreak);

public sealed record ErpPatientTrend(
    [property: JsonPropertyName("patient_id")] Guid PatientId,
    [property: JsonPropertyName("patient_name")] string PatientName,
    [property: JsonPropertyName("enrollment_id")] Guid EnrollmentId,
    [property: JsonPropertyName("delta_pct")] decimal DeltaPct,
    [property: JsonPropertyName("current_pct")] decimal CurrentPct,
    [property: JsonPropertyName("previous_pct")] decimal PreviousPct);

public sealed record ErpPhaseBucket(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("count")] int Count);

public sealed record ErpDailyValue(
    [property: JsonPropertyName("date")] DateOnly Date,
    [property: JsonPropertyName("value")] int Value);

public sealed record ErpClinicalDailyAvg(
    [property: JsonPropertyName("date")] DateOnly Date,
    [property: JsonPropertyName("bmi_avg")] decimal? BmiAvg,
    [property: JsonPropertyName("hba1c_avg")] decimal? Hba1cAvg,
    [property: JsonPropertyName("body_fat_avg")] decimal? BodyFatAvg);

public sealed record ProgramErpDashboardDto(
    [property: JsonPropertyName("kpis")] ErpDashboardKpis Kpis,
    [property: JsonPropertyName("adherencia_por_mision_hoy")] IReadOnlyList<ErpMissionAdherence> AdherenciaPorMisionHoy,
    [property: JsonPropertyName("distribucion_rachas")] IReadOnlyList<ErpStreakBucket> DistribucionRachas,
    [property: JsonPropertyName("xp_por_categoria")] IReadOnlyList<ErpXpByCategory> XpPorCategoria,
    [property: JsonPropertyName("evolucion_30d")] IReadOnlyList<ErpDailyAdherence> Evolucion30Dias,
    [property: JsonPropertyName("mejoraron")] IReadOnlyList<ErpPatientTrend> Mejoraron,
    [property: JsonPropertyName("empeoraron")] IReadOnlyList<ErpPatientTrend> Empeoraron,
    [property: JsonPropertyName("top5")] IReadOnlyList<ErpLeaderboardEntry> Top5,
    [property: JsonPropertyName("distribucion_semanas")] IReadOnlyList<ErpPhaseBucket> DistribucionSemanas,
    [property: JsonPropertyName("evolucion_xp_30d")] IReadOnlyList<ErpDailyValue> EvolucionXp30D,
    [property: JsonPropertyName("evolucion_clinica_30d")] IReadOnlyList<ErpClinicalDailyAvg> EvolucionClinica30D);

// ===================== Today =====================

public sealed record ErpTodayMissionKpi(
    [property: JsonPropertyName("task_code")] string TaskCode,
    [property: JsonPropertyName("completed")] int Completed,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("pct")] decimal Pct);

public sealed record ErpTodayFeedEntry(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("patient_id")] Guid PatientId,
    [property: JsonPropertyName("patient_name")] string PatientName,
    [property: JsonPropertyName("task_code")] string TaskCode,
    [property: JsonPropertyName("points_awarded")] int PointsAwarded,
    [property: JsonPropertyName("completed_at")] DateTime CompletedAt);

public sealed record ErpCriticalPending(
    [property: JsonPropertyName("patient_id")] Guid PatientId,
    [property: JsonPropertyName("patient_name")] string PatientName,
    [property: JsonPropertyName("enrollment_id")] Guid EnrollmentId,
    [property: JsonPropertyName("current_week")] int CurrentWeek,
    [property: JsonPropertyName("pending_tasks")] int PendingTasks);

public sealed record ErpHeatmapCell(
    [property: JsonPropertyName("task_code")] string TaskCode,
    [property: JsonPropertyName("day_index")] int DayIndex,
    [property: JsonPropertyName("pct")] decimal Pct);

public sealed record ProgramErpTodayDto(
    [property: JsonPropertyName("mission_kpis")] IReadOnlyList<ErpTodayMissionKpi> MissionKpis,
    [property: JsonPropertyName("feed")] IReadOnlyList<ErpTodayFeedEntry> Feed,
    [property: JsonPropertyName("pendientes_criticos")] IReadOnlyList<ErpCriticalPending> PendientesCriticos,
    [property: JsonPropertyName("heatmap_semana")] IReadOnlyList<ErpHeatmapCell> HeatmapSemana);

// ===================== Adherencia =====================

public sealed record ErpWeeklyMissionPct(
    [property: JsonPropertyName("week_start")] DateOnly WeekStart,
    [property: JsonPropertyName("task_code")] string TaskCode,
    [property: JsonPropertyName("pct")] decimal Pct);

public sealed record ErpStreakRankingEntry(
    [property: JsonPropertyName("rank")] int Rank,
    [property: JsonPropertyName("patient_id")] Guid PatientId,
    [property: JsonPropertyName("patient_name")] string PatientName,
    [property: JsonPropertyName("enrollment_id")] Guid EnrollmentId,
    [property: JsonPropertyName("current_streak")] int CurrentStreak,
    [property: JsonPropertyName("longest_streak")] int LongestStreak);

public sealed record PaginatedErpAdherenciaTabla(
    [property: JsonPropertyName("data")] IReadOnlyList<ErpAdherenciaRow> Data,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("pageSize")] int PageSize,
    [property: JsonPropertyName("totalPages")] int TotalPages);

public sealed record ErpAdherenciaRow(
    [property: JsonPropertyName("patient_id")] Guid PatientId,
    [property: JsonPropertyName("patient_name")] string PatientName,
    [property: JsonPropertyName("enrollment_id")] Guid EnrollmentId,
    [property: JsonPropertyName("current_week")] int CurrentWeek,
    [property: JsonPropertyName("current_streak")] int CurrentStreak,
    [property: JsonPropertyName("longest_streak")] int LongestStreak,
    [property: JsonPropertyName("global_pct")] decimal GlobalPct,
    [property: JsonPropertyName("podcast_pct")] decimal PodcastPct,
    [property: JsonPropertyName("vitals_pct")] decimal VitalsPct,
    [property: JsonPropertyName("nut_pct")] decimal NutPct,
    [property: JsonPropertyName("ejercicio_pct")] decimal EjercicioPct,
    [property: JsonPropertyName("nutraceutico_pct")] decimal NutraceuticoPct,
    [property: JsonPropertyName("emocional_pct")] decimal EmocionalPct,
    [property: JsonPropertyName("xp")] int Xp,
    [property: JsonPropertyName("trend")] string Trend);

public sealed record ProgramErpAdherenciaDto(
    [property: JsonPropertyName("tendencia_8_semanas")] IReadOnlyList<ErpWeeklyMissionPct> Tendencia8Semanas,
    [property: JsonPropertyName("ranking_rachas")] IReadOnlyList<ErpStreakRankingEntry> RankingRachas,
    [property: JsonPropertyName("tabla")] PaginatedErpAdherenciaTabla Tabla);

// ===================== Cofres =====================

public sealed record ErpMilestoneCounts(
    [property: JsonPropertyName("streak_7")] int Streak7,
    [property: JsonPropertyName("streak_11")] int Streak11,
    [property: JsonPropertyName("streak_22")] int Streak22,
    [property: JsonPropertyName("streak_50")] int Streak50,
    [property: JsonPropertyName("nb_streak_7")] int NbStreak7,
    [property: JsonPropertyName("nb_streak_11")] int NbStreak11,
    [property: JsonPropertyName("nb_streak_22")] int NbStreak22,
    [property: JsonPropertyName("nb_streak_50")] int NbStreak50);

public sealed record ErpCofresHitos(
    [property: JsonPropertyName("streak_7")] bool Streak7,
    [property: JsonPropertyName("streak_11")] bool Streak11,
    [property: JsonPropertyName("streak_22")] bool Streak22,
    [property: JsonPropertyName("streak_50")] bool Streak50);

public sealed record ErpCofresPatientRow(
    [property: JsonPropertyName("patient_id")] Guid PatientId,
    [property: JsonPropertyName("patient_name")] string PatientName,
    [property: JsonPropertyName("current_streak")] int CurrentStreak,
    [property: JsonPropertyName("total_xp")] int TotalXp,
    [property: JsonPropertyName("level")] string Level,
    [property: JsonPropertyName("next_milestone_days")] int NextMilestoneDays,
    [property: JsonPropertyName("hitos")] ErpCofresHitos Hitos,
    [property: JsonPropertyName("pending_clinical_count")] int PendingClinicalCount,
    [property: JsonPropertyName("nb_current_streak")] int NbCurrentStreak);

public sealed record ProgramErpCofresDto(
    [property: JsonPropertyName("xp_por_categoria")] IReadOnlyList<ErpXpByCategory> XpPorCategoria,
    [property: JsonPropertyName("milestones")] ErpMilestoneCounts Milestones,
    [property: JsonPropertyName("tabla")] IReadOnlyList<ErpCofresPatientRow> Tabla,
    [property: JsonPropertyName("proximos_a_desbloquear")] int ProximosADesbloquear);

// ===================== Patient Overview (360) =====================

public sealed record PatientOverviewEnrollment(
    [property: JsonPropertyName("enrollment_id")] Guid EnrollmentId,
    [property: JsonPropertyName("template_id")] Guid TemplateId,
    [property: JsonPropertyName("template_name")] string? TemplateName,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("timezone")] string Timezone,
    [property: JsonPropertyName("started_at")] DateTime StartedAt,
    [property: JsonPropertyName("current_week")] int CurrentWeek,
    [property: JsonPropertyName("total_weeks")] int TotalWeeks);

public sealed record PatientOverviewStreak(
    [property: JsonPropertyName("current_streak")] int CurrentStreak,
    [property: JsonPropertyName("longest_streak")] int LongestStreak,
    [property: JsonPropertyName("freezes_remaining")] int FreezesRemaining,
    [property: JsonPropertyName("nb_current_streak")] int NbCurrentStreak,
    [property: JsonPropertyName("nb_longest_streak")] int NbLongestStreak);

public sealed record PatientOverviewXp(
    [property: JsonPropertyName("balance")] int Balance,
    [property: JsonPropertyName("level")] string Level,
    [property: JsonPropertyName("next_level_at")] int? NextLevelAt);

public sealed record PatientOverviewTaskHoy(
    [property: JsonPropertyName("task_code")] string TaskCode,
    [property: JsonPropertyName("completed")] bool Completed,
    [property: JsonPropertyName("points")] int Points);

public sealed record PatientOverviewMissionAdherence(
    [property: JsonPropertyName("task_code")] string TaskCode,
    [property: JsonPropertyName("pct")] decimal Pct);

public sealed record PatientOverviewWeeklyEvo(
    [property: JsonPropertyName("week_start")] DateOnly WeekStart,
    [property: JsonPropertyName("pct")] decimal Pct,
    [property: JsonPropertyName("xp")] int Xp);

public sealed record PatientOverviewHealthScore(
    [property: JsonPropertyName("score")] int Score,
    [property: JsonPropertyName("score_adherence")] int ScoreAdherence,
    [property: JsonPropertyName("score_clinical")] int ScoreClinical,
    [property: JsonPropertyName("score_nutrition")] int ScoreNutrition,
    [property: JsonPropertyName("score_psychology")] int ScorePsychology,
    [property: JsonPropertyName("score_exercise")] int ScoreExercise,
    [property: JsonPropertyName("trend")] string Trend);

public sealed record PatientOverviewTransformationDetail(
    [property: JsonPropertyName("baseline")] decimal Baseline,
    [property: JsonPropertyName("current")] decimal Current,
    [property: JsonPropertyName("unit")] string Unit,
    [property: JsonPropertyName("delta")] decimal Delta,
    [property: JsonPropertyName("delta_pct")] decimal DeltaPct,
    [property: JsonPropertyName("favorable")] bool Favorable,
    [property: JsonPropertyName("score")] int Score);

public sealed record PatientOverviewTransformationScore(
    [property: JsonPropertyName("score")] int Score,
    [property: JsonPropertyName("week_number")] int WeekNumber,
    [property: JsonPropertyName("overall_trend")] string OverallTrend,
    [property: JsonPropertyName("previous")] int? Previous = null,
    [property: JsonPropertyName("detail")] IReadOnlyDictionary<string, PatientOverviewTransformationDetail>? Detail = null);

public sealed record PatientOverviewScores(
    [property: JsonPropertyName("health")] PatientOverviewHealthScore? Health,
    [property: JsonPropertyName("transformation")] PatientOverviewTransformationScore? Transformation);

public sealed record ErpWeaknessSummary(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("detected_at")] DateTime DetectedAt);

public sealed record ErpInterventionSummary(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt);

public sealed record ErpClinicalReviewSummary(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("rule_code")] string RuleCode,
    [property: JsonPropertyName("metric_id")] string MetricId,
    [property: JsonPropertyName("delta_pct")] decimal? DeltaPct,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt);

public sealed record ErpDailyCheckinSummary(
    [property: JsonPropertyName("local_date")] DateOnly LocalDate,
    [property: JsonPropertyName("total_points")] int TotalPoints,
    [property: JsonPropertyName("bonus_awarded")] int BonusAwarded,
    [property: JsonPropertyName("is_perfect_day")] bool IsPerfectDay,
    [property: JsonPropertyName("mood_score")] short? MoodScore);

// ===================== Clinical Metrics (Patient Overview) =====================

public sealed record PatientOverviewMetricPoint(
    [property: JsonPropertyName("date")] DateTime Date,
    [property: JsonPropertyName("value")] decimal Value);

public sealed record PatientOverviewMetricSnapshot(
    [property: JsonPropertyName("latest_value")] decimal? LatestValue,
    [property: JsonPropertyName("unit")] string? Unit,
    [property: JsonPropertyName("observed_at")] DateTime? ObservedAt,
    [property: JsonPropertyName("baseline_value")] decimal? BaselineValue,
    [property: JsonPropertyName("delta_pct")] decimal? DeltaPct,
    [property: JsonPropertyName("series_12w")] IReadOnlyList<PatientOverviewMetricPoint> Series12W);

public sealed record PatientOverviewClinicalMetricsDto(
    [property: JsonPropertyName("bmi")] PatientOverviewMetricSnapshot? Bmi,
    [property: JsonPropertyName("hba1c")] PatientOverviewMetricSnapshot? Hba1c,
    [property: JsonPropertyName("body_fat")] PatientOverviewMetricSnapshot? BodyFat,
    [property: JsonPropertyName("glucose")] PatientOverviewMetricSnapshot? Glucose = null);

public sealed record PatientOverviewDto(
    [property: JsonPropertyName("patient_name")] string PatientName,
    [property: JsonPropertyName("enrollment")] PatientOverviewEnrollment? Enrollment,
    [property: JsonPropertyName("streak")] PatientOverviewStreak? Streak,
    [property: JsonPropertyName("xp")] PatientOverviewXp? Xp,
    [property: JsonPropertyName("tareas_hoy")] IReadOnlyList<PatientOverviewTaskHoy> TareasHoy,
    [property: JsonPropertyName("adherencia_semana")] IReadOnlyList<PatientOverviewMissionAdherence> AdherenciaSemana,
    [property: JsonPropertyName("evolucion_12_semanas")] IReadOnlyList<PatientOverviewWeeklyEvo> Evolucion12Semanas,
    [property: JsonPropertyName("scores")] PatientOverviewScores? Scores,
    [property: JsonPropertyName("weaknesses")] IReadOnlyList<ErpWeaknessSummary> Weaknesses,
    [property: JsonPropertyName("interventions")] IReadOnlyList<ErpInterventionSummary> Interventions,
    [property: JsonPropertyName("pending_adaptations")] int PendingAdaptations,
    [property: JsonPropertyName("clinical_reviews")] IReadOnlyList<ErpClinicalReviewSummary> ClinicalReviews,
    [property: JsonPropertyName("daily_checkins")] IReadOnlyList<ErpDailyCheckinSummary> DailyCheckins,
    [property: JsonPropertyName("mediciones_clinicas")] PatientOverviewClinicalMetricsDto? MedicionesClinicas);
