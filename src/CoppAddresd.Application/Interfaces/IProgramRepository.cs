using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.ProgramProgress.Commands.ReconcileStreaks;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.ActivityLog;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.ClinicalXp;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.Erp;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.Interventions;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.Nutrition;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.Scores;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.Weaknesses;
using CoppAddresd.Application.Features.ProgramProgress.Queries.ExportEnrollments;
using CoppAddresd.Application.Services.ProgramProgress;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Repositorio del módulo Progreso del Programa (83 semanas). Persistencia de
/// inscripciones, semanas, completaciones con XP/racha idempotentes, plantillas
/// y recomendaciones de adaptación (SPEC §6 y §7).
///
/// Reglas de la implementación (TASKS T-07):
/// - Una transacción = un agregado. Completar una tarea escribe
///   <c>task_completions</c> + <c>xp_ledger</c> + <c>daily_checkins</c> +
///   <c>streak_states</c>/<c>streak_freezes</c> dentro de un único
///   <c>FOR UPDATE</c> sobre <c>program_enrollments</c> (decisión 8).
/// - Toda lectura sin mutación es <c>AsNoTracking</c>; solo la fila de la
///   inscripción bloqueada se rastrea en <c>CompleteTaskAsync</c>.
/// </summary>
public interface IProgramRepository
{
    // --- Inscripciones ---

    /// <summary>
    /// Crea la inscripción activa, su fila de racha y las semanas del programa
    /// (semana 1 activa con snapshot; el resto bloqueadas con snapshot vacío
    /// que se toma al activar, SPEC §4.5/§5.2). Una inscripción activa por
    /// paciente (índice único parcial): si ya existe, violación → 409.
    /// </summary>
    Task<ProgramEnrollment> EnrollAsync(
        Guid patientId,
        Guid templateId,
        string timezone,
        DateOnly startLocalDate,
        Guid? createdBy = null,
        CancellationToken ct = default
    );

    /// <summary>Pausa la inscripción (Active → Paused). Requiere estado Active.</summary>
    Task<ProgramEnrollment> PauseAsync(
        Guid enrollmentId,
        Guid? actorId = null,
        CancellationToken ct = default
    );

    /// <summary>Reanuda la inscripción (Paused → Active). Requiere estado Paused.</summary>
    Task<ProgramEnrollment> ResumeAsync(
        Guid enrollmentId,
        Guid? actorId = null,
        CancellationToken ct = default
    );

    /// <summary>Retira la inscripción (terminal, conserva historial). No admite Completed/Withdrawn.</summary>
    Task<ProgramEnrollment> WithdrawAsync(
        Guid enrollmentId,
        Guid? actorId = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Proyección de una inscripción con su estado de gamificación (racha,
    /// congelamientos, balance XP y semanas totales de la plantilla), usada por
    /// los handlers de enroll/pause/resume/withdraw y por el listado del ERP.
    /// Devuelve null si la inscripción no existe.
    /// </summary>
    Task<ProgramEnrollmentDto?> GetEnrollmentAsync(
        Guid enrollmentId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Listado paginado de inscripciones con filtros opcionales por paciente y
    /// estado (SPEC §7.5, ERP). Ordena por creación descendente.
    /// <paramref name="scopedPatientIds"/> (T-81): filtro opcional de scoping
    /// del actor — lista con ids = solo esas inscripciones; <c>null</c> = sin
    /// filtro. La lista vacía se deniega antes, en el handler.
    /// </summary>
    Task<(IReadOnlyList<ProgramEnrollmentDto> Items, int Total)> ListEnrollmentsAsync(
        Guid? patientId,
        ProgramEnrollmentStatus? status,
        int page,
        int pageSize,
        IReadOnlyList<Guid>? scopedPatientIds = null,
        CancellationToken ct = default
    );

    // --- Completación de tareas ---

    /// <summary>
    /// Persiste una tarea completada de forma idempotente dentro de una
    /// transacción con la inscripción bloqueada <c>FOR UPDATE</c>:
    /// <c>task_completions</c> + <c>xp_ledger</c> (TaskCompletion y DailyBonus
    /// si el día queda perfecto) + <c>daily_checkins</c> +
    /// <c>streak_states</c>/<c>streak_freezes</c>.
    ///
    /// El resultado distingue primera escritura (Created), replay idempotente
    /// (Replay, sin doble XP) y clave de idempotencia reutilizada con otra
    /// tarea/fecha (IdempotencyKeyReused → 409 AC-04).
    /// </summary>
    Task<CompleteTaskResult> CompleteTaskAsync(
        CompleteTaskInput input,
        CancellationToken ct = default
    );

    // --- Lecturas del paciente ---

    /// <summary>
    /// Proyección del snapshot completo (SPEC §7.1) en pocas queries
    /// set-based (sin N+1). Devuelve null si la inscripción no existe.
    /// </summary>
    Task<ProgramSnapshotDto?> GetSnapshotAsync(
        Guid enrollmentId,
        DateOnly todayLocalDate,
        CancellationToken ct = default
    );

    /// <summary>
    /// Hoy en zona local del paciente para una inscripción (SPEC §6.11), o null
    /// si la inscripción no existe. Lo usan los handlers de completación
    /// (chequeo "due today") y del snapshot cuando el cliente no envía fecha.
    /// </summary>
    Task<DateOnly?> GetPatientLocalTodayAsync(Guid enrollmentId, CancellationToken ct = default);

    /// <summary>Rollups diarios de la ventana [from, to] (SPEC §7.3).</summary>
    Task<ProgramCalendarDto> GetCalendarAsync(
        Guid enrollmentId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default
    );

    /// <summary>Sendero completo de semanas (SPEC §7.4).</summary>
    Task<ProgramPathDto> GetPathAsync(Guid enrollmentId, CancellationToken ct = default);

    // --- Plantillas (ERP) ---

    Task<(IReadOnlyList<ProgramTemplate> Items, int Total)> ListTemplatesAsync(
        string? search,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    /// <summary>Plantilla con sus filas por día ordenadas.</summary>
    Task<ProgramTemplate?> GetTemplateAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Busca una plantilla por código (resolución de la plantilla por defecto
    /// al inscribir, SPEC §8.4: <c>Program:DefaultTemplate:Code</c>, fallback
    /// <c>program-coppaddresd-83-days</c>, el programa 83 días / 12 semanas).
    /// Devuelve null si no existe.
    /// </summary>
    Task<ProgramTemplate?> GetTemplateByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// Inserta o actualiza una plantilla reemplazando sus <c>weekly_day_templates</c>.
    /// No toca <c>version</c> (solo publish la incrementa, SPEC §7.6): el handler
    /// la ajusta antes de llamar si corresponde.
    /// </summary>
    Task<ProgramTemplate> UpsertTemplateAsync(
        ProgramTemplate template,
        IReadOnlyList<WeeklyDayTemplate> dayTemplates,
        Guid? actorId = null,
        CancellationToken ct = default
    );

    /// <summary>Reemplazo en bloque de las tareas por día de una plantilla.</summary>
    Task<IReadOnlyList<WeeklyDayTemplate>> ReplaceWeekdayTasksAsync(
        Guid templateId,
        IReadOnlyList<WeeklyDayTemplate> tasks,
        Guid? actorId = null,
        CancellationToken ct = default
    );

    /// <summary>Reemplazo en bloque del TasksSnapshot de una semana específica en la inscripción del paciente.</summary>
    Task<EnrollmentWeekDetailDto> ReplaceEnrollmentWeekTasksAsync(
        Guid enrollmentId,
        int weekNumber,
        IReadOnlyList<WeeklyDayTemplate> tasks,
        Guid? actorId = null,
        CancellationToken ct = default
    );

    // --- Adaptaciones (ERP) ---

    Task<(IReadOnlyList<AdaptationRecommendation> Items, int Total)> ListAdaptationsAsync(
        Guid? enrollmentId,
        AdaptationStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    Task<AdaptationRecommendation?> GetAdaptationAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Transiciones de la máquina de estados (SPEC §5.6):
    /// Pending → Approved|Rejected (Approve/Reject), Approved → Applied (Apply).
    /// Cuando la transición deja la recomendación en <c>Applied</c> y
    /// <paramref name="auditActionOnApply"/> no es nulo, la fila semántica
    /// (p. ej. <c>AdaptationApplied</c>) se inserta en <c>audit.activity_logs</c>
    /// en la MISMA transacción que el cambio de estado (AC-17): si el commit
    /// falla, ni la transición ni la fila quedan a medias. El handler ya no
    /// audita post-commit.
    /// </summary>
    Task<AdaptationRecommendation> DecideAdaptationAsync(
        Guid adaptationId,
        AdaptationDecisionAction action,
        Guid? actorId = null,
        string? auditActionOnApply = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Marca como <c>Superseded</c> las recomendaciones <c>Pending</c> previas de
    /// la inscripción con el mismo <c>(kind, target_entity_id)</c> al crear una
    /// nueva (SPEC §5.6: una Pending nueva reemplaza a la anterior en la misma
    /// transacción). Excluye la fila recién creada
    /// (<paramref name="newRecommendationId"/>) para no auto-superarla. Devuelve
    /// cuántas filas quedaron superadas.
    /// </summary>
    Task<int> SupersedePendingAsync(
        Guid enrollmentId,
        AdaptationKind kind,
        Guid targetEntityId,
        Guid newRecommendationId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Registra una fila semántica en <c>audit.activity_logs</c> (SPEC §5.6 y
    /// §6.8, AC-17: <c>action = 'AdaptationApplied'</c>). El trigger de
    /// auditoría cubre el DML de las tablas del módulo; esta fila es un evento
    /// semántico explícito que el trigger no puede generar, y la capa de
    /// aplicación nunca toca <c>AppDbContext</c>, por lo que la escribe el
    /// repositorio con SQL parametrizado (precedente: <c>FOR UPDATE</c> en
    /// <c>CompleteTaskAsync</c>).
    /// </summary>
    Task WriteAuditRowAsync(
        string action,
        string schemaName,
        string tableName,
        Guid recordId,
        Guid? actorId = null,
        CancellationToken ct = default
    );

    // --- Motor de puntajes (SPEC §13, T-37) ---

    /// <summary>
    /// Índice de Salud del período del paciente (SPEC §13.2/§13.3):
    /// compute-on-read. Si existe una fila fresca en
    /// <c>(patient_id, period_start, period_end)</c> (su <c>period_end</c> es
    /// hoy o futuro) se devuelve la persistida; si falta o está vencida se
    /// calcula con <see cref="IHealthScoreCalculator"/> y se persiste una fila
    /// nueva con <c>score_previous</c> desde la fila anterior. Con
    /// <paramref name="force"/> se recalcula SIEMPRE y se actualiza la fila del
    /// período (recálculo manual clínico). Devuelve null si el paciente no
    /// tiene inscripción activa.
    /// </summary>
    /// <param name="periodEndLocalDate">
    /// Fin del período en fecha local del paciente (SPEC §13.7.2, recálculo
    /// manual). Opcional: si no se envía, el fin es el "hoy" local. Una fecha
    /// futura respecto del hoy local del paciente se rechaza con
    /// <c>422 INVALID_PERIOD</c> (el repositorio es la autoridad del tiempo
    /// local, nunca confía en <c>DateTime.UtcNow</c> del handler).
    /// </param>
    Task<HealthScoreDto?> GetOrComputeHealthScoreAsync(
        Guid patientId,
        ScoreTrigger trigger,
        bool force = false,
        DateOnly? periodEndLocalDate = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Índice de Transformación de la semana actual del paciente (SPEC
    /// §13.2/§13.5): compute-on-read por <c>week_number</c>. Si existe una fila
    /// para la semana actual se devuelve la persistida; si no, se calcula con
    /// <see cref="ITransformationScoreCalculator"/> y se persiste con
    /// <c>score_previous</c> desde la fila anterior. Con
    /// <paramref name="force"/> se recalcula y actualiza la fila de la semana.
    /// Devuelve null si el paciente no tiene inscripción activa.
    /// </summary>
    Task<TransformationScoreDto?> GetOrComputeTransformationScoreAsync(
        Guid patientId,
        ScoreTrigger trigger,
        bool force = false,
        CancellationToken ct = default
    );

    /// <summary>Líneas base clínicas del paciente (SPEC §13.1.2, lectura).</summary>
    Task<IReadOnlyList<ClinicalBaselineDto>> ListClinicalBaselinesAsync(
        Guid patientId,
        CancellationToken ct = default
    );

    /// <summary>
    /// UPSERT de una línea base clínica con la guardia AC-22: <c>set_by</c> es
    /// obligatorio y el llamador debe tener un rol clínico
    /// (<c>Physician</c>, <c>Nutritionist</c>, <c>Psychologist</c>,
    /// <c>ClinicalDirector</c> o <c>Admin</c>); un paciente auto-asignándose →
    /// <see cref="Domain.Exceptions.ForbiddenException"/> (403). Valida
    /// <c>value &gt; 0</c> y <c>target_value &gt; 0</c> dentro de límites
    /// razonables de la métrica. Único por <c>(patient_id, metric_id)</c>:
    /// la segunda escritura actualiza la fila existente.
    /// </summary>
    Task<ClinicalBaselineDto> UpsertClinicalBaselineAsync(
        ClinicalBaselineWrite input,
        IReadOnlyList<string> callerRoles,
        CancellationToken ct = default
    );

    /// <summary>
    /// Valor de la medición más reciente de una métrica en la ventana de fechas
    /// locales del paciente, o null si no hay mediciones en la ventana. Lo usan
    /// los cálculos del motor de puntajes (una métrica por llamada, sin N+1 en
    /// las rutas de cálculo que agrupan por métrica).
    /// </summary>
    Task<decimal?> GetLatestMeasurementAsync(
        Guid patientId,
        Guid metricId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct = default
    );

    // --- XP clínica (SPEC §15, T-46..T-49) ---

    /// <summary>
    /// Evalúa la evolución clínica del período contra las líneas base y
    /// produce los otorgamientos/revisiones de XP clínica (SPEC §15, C). Se
    /// invoca SOLO desde <c>POST /scores/calculate</c> (recálculo manual
    /// clínico), NUNCA desde <c>GET /scores</c> (el GET solo computa puntajes
    /// y devuelve la cola de revisiones pendientes).
    ///
    /// Por métrica con medición en el período (misma ventana que el Índice de
    /// Transformación):
    /// - favorable y |Δ%| ≥ umbral de significancia (default 5%,
    ///   configurable) → crea/reutiliza una revisión <c>pending</c> en
    ///   <c>app.clinical_xp_reviews</c> (NO otorga XP aún; la aprueba un
    ///   clínico).
    /// - favorable y 1% ≤ |Δ%| &lt; umbral → auto-otorga <c>CLINICAL_IMPROVE</c>.
    /// - |Δ%| &lt; 1% → auto-otorga <c>CLINICAL_STABLE</c>.
    /// - desfavorable → NO otorga nada (0 XP; nunca penaliza, SPEC §15).
    /// - si TODAS las métricas con medición son favorables (y hay al menos
    ///   una) → auto-otorga <c>CLINICAL_WEEKLY_ALL_UP</c> una vez por período.
    ///
    /// Idempotencia: el dedupe parcial <c>(source_ref_type, source_ref_id,
    /// reason)</c> de <c>xp_ledger</c> con <c>source_ref_type =
    /// 'clinical_period'</c> y <c>source_ref_id = health_scores.id</c> evita
    /// el doble otorgamiento por período y regla (violación única → se omite).
    /// </summary>
    Task<ClinicalXpEvaluationResult> EvaluateClinicalXpAwardsAsync(
        Guid patientId,
        DateOnly? periodEndLocalDate = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Cola paginada de revisiones clínicas de XP pendientes (SPEC §15, D):
    /// mejorías significativas que esperan decisión de un clínico
    /// (<c>status = 'pending'</c>). Trae paciente, métrica (código/nombre),
    /// |Δ%| y el período del Índice de Salud. Ordena por creación ascendente
    /// (FIFO de la cola clínica).
    /// </summary>
    Task<(IReadOnlyList<ClinicalReviewDto> Items, int Total)> ListPendingClinicalReviewsAsync(
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    /// <summary>
    /// Decide una revisión clínica de XP (SPEC §15, D): requiere que el
    /// llamador tenga un rol clínico (<c>Physician</c>, <c>Nutritionist</c>,
    /// <c>Psychologist</c>, <c>ClinicalDirector</c> o <c>Admin</c>) — misma
    /// guardia AC-22 que <see cref="UpsertClinicalBaselineAsync"/> (un paciente
    /// decidir su propia XP significativa → 403 FORBIDDEN).
    ///
    /// - <paramref name="approve"/> = true: marca <c>approved</c> con
    ///   <c>decided_by</c>/<c>decided_at</c> y otorga <c>CLINICAL_SIGNIFICANT</c>
    ///   con <c>validated_by = actorId</c> y <c>validated_at = now</c> (mismo
    ///   dedupe <c>clinical_period</c>; si el período ya tiene el otorgamiento,
    ///   se omite el doble).
    /// - <paramref name="approve"/> = false: marca <c>rejected</c> con
    ///   <c>decided_by</c>/<c>decided_at</c>; sin XP.
    /// - Ya decidida → <see cref="Domain.Exceptions.BusinessRuleViolationException"/>
    ///   <c>REVIEW_ALREADY_DECIDED</c> (409).
    /// </summary>
    Task<ClinicalReviewDto> DecideClinicalXpReviewAsync(
        Guid reviewId,
        bool approve,
        Guid? actorId,
        IReadOnlyList<string> callerRoles,
        CancellationToken ct = default
    );

    // --- Nutrición granular (SPEC §18, "Paso 6") ---

    /// <summary>
    /// Registra una comida o hidratación del paciente (SPEC §18, B): crea el
    /// <c>app.habit_checks</c> de <c>(paciente, plantilla de hábito, fecha
    /// local)</c> — idempotente, único por tripleta — y otorga la XP granular
    /// (<c>NUTRITION_MEAL_COMPLETE</c> para comidas,
    /// <c>NUTRITION_HYDRATION</c> para hidratación) por el camino de resolución
    /// del catálogo (SPEC §14.3 + §16: multiplicador del paciente y topes
    /// <c>max_per_day</c>/<c>max_per_week</c>).
    ///
    /// El <c>localDate</c> opcional se resuelve contra el hoy local del
    /// paciente (una fecha futura → 422 <c>INVALID_DATE</c>). Un log duplicado
    /// de una comida (des/alm/mer/cen) →
    /// <see cref="Domain.Exceptions.BusinessRuleViolationException"/>
    /// <c>HABIT_ALREADY_LOGGED</c> (409); la XP nunca se duplica (dedupe
    /// parcial <c>('habit_log', habit_check.id, reason)</c> como backstop de
    /// carrera). El <c>agua</c> es acumulable: repetir el mismo día responde
    /// 200 con XP 0 y actualiza el total acumulado <c>waterMl</c> de la fila de
    /// intake anclada al habit_check existente (solo sube — monotónico;
    /// menor/igual o sin <c>waterMl</c> → no-op idempotente; fila faltante →
    /// se crea sin XP), mientras la XP de hidratación sigue siendo 1/día
    /// (primer log). Paciente sin inscripción activa → 404
    /// <c>NO_ACTIVE_ENROLLMENT</c>.
    ///
    /// Con <paramref name="intake"/> (SPEC nutrition-intake-adherence) además
    /// persiste la fila de <c>app.nutrition_intake_logs</c> anclada al
    /// <c>habit_check</c> de la misma transacción, resuelve el plan-day activo
    /// server-side (referencia null sin plan) y verifica la ownership del
    /// <c>foodAnalysisId</c> contra el <paramref name="actorId"/> (subject del
    /// JWT, D6): análisis inexistente o de otro usuario → 422
    /// <c>FOOD_ANALYSIS_*</c>; análisis anónimo → aceptado en v1.
    /// </summary>
    Task<NutritionLogResultDto> LogNutritionAsync(
        Guid patientId,
        MealCode mealCode,
        DateOnly? localDate = null,
        NutritionIntakePayload? intake = null,
        Guid? actorId = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Actualiza el intake de un log nutricional EXISTENTE (edición del móvil):
    /// misma <c>HabitCheck</c> (sin duplicados), recalcula la fila de
    /// <c>app.nutrition_intake_logs</c> con los valores del payload
    /// (reemplazo completo, null limpia) y conserva las referencias del plan.
    /// Sin XP (el otorgamiento vive solo en el primer log). Sin log para esa
    /// comida/fecha → 404 <c>NUTRITION_LOG_NOT_FOUND</c>. Misma resolución de
    /// fecha local, plantilla y ownership de <c>foodAnalysisId</c> que el POST.
    /// </summary>
    Task<NutritionLogResultDto> UpdateNutritionIntakeAsync(
        Guid patientId,
        MealCode mealCode,
        DateOnly? localDate = null,
        NutritionIntakePayload? intake = null,
        Guid? actorId = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Otorgamientos semanales de nutrición (SPEC §18, C): se evalúan SOLO en
    /// <c>POST /scores/calculate</c> (nunca en <c>GET /scores</c>), después de
    /// persistir la fila de <c>health_scores</c> del período. Usa la MISMA
    /// fuente de adherencia que la dimensión de nutrición del Índice de Salud
    /// (<c>app.habit_checks</c> categoría <c>alimentacion</c>; reutiliza la
    /// lógica de <c>GetNutritionLogAsync</c>, no la duplica):
    ///
    /// - adherencia ≥ 85% → auto-otorga <c>NUTRITION_WEEK_85</c> (75 XP) una
    ///   vez por período.
    /// - adherencia sube ≥ 20 puntos porcentuales vs el período anterior (la
    ///   fila previa de <c>health_scores</c>) → auto-otorga
    ///   <c>NUTRITION_RECOVERY</c> (50 XP) una vez por período.
    ///
    /// Idempotencia: el dedupe parcial <c>(source_ref_type, source_ref_id,
    /// reason)</c> de <c>xp_ledger</c> con <c>source_ref_type =
    /// 'nutrition_period'</c> y <c>source_ref_id = health_scores.id</c> limita
    /// a un otorgamiento por regla y período (violación única → se omite).
    /// Respeta el multiplicador del paciente (SPEC §16, C.4) como todo
    /// otorgamiento.
    /// </summary>
    Task<NutritionWeeklyAwardsResult> EvaluateNutritionAwardsAsync(
        Guid patientId,
        DateOnly? periodEndLocalDate = null,
        CancellationToken ct = default
    );

    // --- Detección de debilidades (SPEC §21, "Paso 7c") ---

    /// <summary>
    /// Paquete de datos semanales del paciente para el motor de detección
    /// (SPEC §21, B): el repositorio reúne la ventana del período (misma que el
    /// Índice de Salud, SPEC §13.2) con queries set-based. Indicadores sin dato
    /// físico quedan null (nunca penaliza por ausencia de datos). Devuelve null
    /// si el paciente no tiene inscripción activa. El
    /// <c>periodEndLocalDate</c> opcional se valida contra el hoy local
    /// (futura → 422 <c>INVALID_PERIOD</c>).
    /// </summary>
    Task<PatientWeeklyData?> BuildPatientWeeklyDataAsync(
        Guid patientId,
        DateOnly? periodEndLocalDate = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Persiste las debilidades NUEVAS detectadas (SPEC §21, C — AC-43): omite
    /// los descriptores cuyo <c>code</c> ya tiene una fila
    /// <c>open</c>/<c>acknowledged</c>/<c>in_intervention</c> del mismo
    /// paciente (sin duplicados mientras estén abiertas). Las filas se crean
    /// con <c>source = 'ai'</c> y <c>status = 'open'</c>. Devuelve las
    /// debilidades nuevas persistidas (con sus IDs) para que el servicio de
    /// detección pueda crear intervenciones derivadas.
    /// </summary>
    Task<IReadOnlyList<Weakness>> PersistDetectedWeaknessesAsync(
        Guid patientId,
        IReadOnlyList<WeaknessDescriptor> descriptors,
        CancellationToken ct = default
    );

    /// <summary>
    /// Debilidades del paciente (SPEC §21, D): listado paginado ordenado por
    /// <c>detected_at</c> descendente (vista del móvil/ERP del paciente).
    /// </summary>
    Task<(IReadOnlyList<WeaknessDto> Items, int Total)> ListWeaknessesAsync(
        Guid patientId,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    /// <summary>
    /// Cola clínica de debilidades abiertas (SPEC §21, D): listado paginado de
    /// las filas <c>status = 'open'</c> ordenadas por <c>detected_at</c>
    /// ascendente (FIFO de la cola clínica, misma semántica que las revisiones
    /// de XP pendientes).
    /// </summary>
    Task<(IReadOnlyList<WeaknessDto> Items, int Total)> ListOpenWeaknessesAsync(
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    /// <summary>
    /// Transición de estado de una debilidad (SPEC §21, D — AC-44): requiere
    /// que el llamador tenga un rol clínico (<c>Physician</c>,
    /// <c>Nutritionist</c>, <c>Psychologist</c>, <c>ClinicalDirector</c> o
    /// <c>Admin</c>) — misma guardia AC-22 que la línea base clínica y la
    /// revisión de XP (un paciente cambiando el estado de su propia debilidad →
    /// 403 FORBIDDEN). Estados válidos: <c>acknowledged</c>,
    /// <c>in_intervention</c>, <c>resolved</c> (fija <c>resolved_at</c>) y
    /// <c>dismissed</c>. Transición idempotente: aplicar el mismo estado
    /// devuelve la fila sin error. Desconocida → 404.
    /// </summary>
    Task<WeaknessDto> UpdateWeaknessStatusAsync(
        Guid weaknessId,
        WeaknessStatus status,
        Guid? actorId,
        IReadOnlyList<string> callerRoles,
        CancellationToken ct = default
    );

    // --- Intervenciones (SPEC §22, "Paso 7d") ---

    /// <summary>
    /// Crea una intervención desde una debilidad (SPEC §22, C — AC-46): verifica
    /// que no exista ya una intervención vinculada a esa debilidad (una por
    /// debilidad). La debilidad se transiciona a <c>in_intervention</c> si estaba
    /// en <c>open</c>/<c>acknowledged</c>. Otorga <c>WEAKNESS_ASSESS</c> (+20)
    /// una vez (dedupe parcial <c>'intervention'</c>). Devuelve la intervención
    /// creada o la existente si ya había una.
    /// </summary>
    Task<Intervention> EnsureInterventionFromWeaknessAsync(
        Guid patientId,
        Guid weaknessId,
        InterventionType type,
        string title,
        string? description,
        Guid? actorId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Intervenciones del paciente (SPEC §22, D): listado paginado ordenado por
    /// <c>created_at</c> descendente (vista del móvil del paciente).
    /// </summary>
    Task<(IReadOnlyList<InterventionDto> Items, int Total)> ListInterventionsAsync(
        Guid patientId,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    /// <summary>
    /// Cola clínica de intervenciones abiertas (SPEC §22, D): listado paginado
    /// de filas con <c>status != 'completed'</c> ordenadas por <c>created_at</c>
    /// ascendente (FIFO).
    /// </summary>
    Task<(IReadOnlyList<InterventionDto> Items, int Total)> ListOpenInterventionsAsync(
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    /// <summary>
    /// Paciente acepta una intervención (SPEC §22, D — AC-47):
    /// <c>detected→accepted</c>, fija <c>accepted_at</c>, otorga
    /// <c>INTERV_ACCEPT</c> (+15); si es <c>recovery_mission</c> también
    /// <c>RECOVERY_MISSION</c> (+50). Requiere que la intervención pertenezca
    /// al paciente. Estado inválido → 409.
    /// </summary>
    Task<InterventionDto> AcceptInterventionAsync(
        Guid interventionId,
        Guid patientId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Clínico actualiza el estado de una intervención (SPEC §22, D — AC-48):
    /// transiciones con auditoría; <c>completed</c> REQUIRES resultado y otorga
    /// <c>INTERV_COMPLETE</c> (+200, validated_by = clínico) + debilidad
    /// vinculada → <c>resolved</c>. <c>in_progress</c> es el estado por defecto
    /// tras tele-asistencia confirmada. Estado inválido → 409.
    /// </summary>
    Task<InterventionDto> UpdateInterventionStatusAsync(
        Guid interventionId,
        InterventionStatus status,
        Guid? actorId,
        IReadOnlyList<string> callerRoles,
        string? result = null,
        Guid? assignedTo = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Hook de telemedicina: teleconsulta agendada (SPEC §22, D — AC-49):
    /// otorga <c>TELE_SCHEDULE</c> (+50) y fija una nota en la intervención.
    /// No cambia el estado.
    /// </summary>
    Task<InterventionDto> MarkTeleScheduledAsync(
        Guid interventionId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Hook de telemedicina: asistencia confirmada por el clínico
    /// (SPEC §22, D — AC-49): otorga <c>TELE_ATTEND</c> (+100, validated_by =
    /// clínico) y mueve la intervención a <c>in_progress</c>.
    /// </summary>
    Task<InterventionDto> MarkTeleAttendedAsync(
        Guid interventionId,
        Guid clinicianId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Hook de telemedicina: cumplimiento evaluado (SPEC §22, D — AC-49):
    /// otorga <c>TELE_COMPLY</c> (+50) y puede avanzar hacia
    /// <c>completed</c>.
    /// </summary>
    Task<InterventionDto> MarkTeleComplyAsync(Guid interventionId, CancellationToken ct = default);

    // --- T-77: Helpers para el configurador de contenido ---

    /// <summary>
    /// Nombre y código de un plan de alimentación por ID (T-77). Devuelve null
    /// si el plan no existe. Usado por el handler de GetProgramContent para
    /// armar las referencias de las semanas.
    /// </summary>
    Task<(string Code, string Name)?> GetPlanNameAsync(Guid planId, CancellationToken ct = default);

    /// <summary>
    /// Nombre y código de una rutina de ejercicio por ID (T-77). Devuelve null
    /// si la rutina no existe. Usado por el handler de GetProgramContent.
    /// </summary>
    Task<(string Code, string Name)?> GetRoutineNameAsync(
        Guid routineId,
        CancellationToken ct = default
    );

    // --- Detalle de semana (GET /enrollments/{id}/week/{weekNumber}) ---

    /// <summary>
    /// Detalle de una semana específica de una inscripción: tareas del snapshot,
    /// completaciones reales, rollup diario y verificación de scoping clínico.
    /// Devuelve null si la inscripción no existe, la semana no existe o el
    /// profesional no tiene asignación activa con el paciente.
    /// </summary>
    Task<EnrollmentWeekDetailDto?> GetEnrollmentWeekDetailAsync(
        Guid enrollmentId,
        int weekNumber,
        Guid clinicianUserId,
        CancellationToken ct = default
    );

    // --- ERP gamificación (SPEC §23) ---

    /// <summary>Dashboard ERP del monitoreo comunitario (SPEC §23, AC-50).</summary>
    Task<ProgramErpDashboardDto> GetErpDashboardAsync(CancellationToken ct = default);

    /// <summary>Vista de hoy para el ERP (SPEC §23, AC-51).</summary>
    Task<ProgramErpTodayDto> GetErpTodayAsync(CancellationToken ct = default);

    /// <summary>Vista de adherencia ERP con tabla paginada (SPEC §23, AC-52).</summary>
    Task<ProgramErpAdherenciaDto> GetErpAdherenciaAsync(
        int page,
        int pageSize,
        string? search,
        string? sortBy,
        string? sortDir,
        CancellationToken ct = default
    );

    /// <summary>Vista de cofres/rachas ERP (SPEC §23, AC-53).</summary>
    Task<ProgramErpCofresDto> GetErpCofresAsync(
        int page = 1,
        int pageSize = 10,
        string? search = null,
        string? sortBy = null,
        string? sortDir = null,
        CancellationToken ct = default
    );

    /// <summary>Perfil 360 de un paciente (SPEC §23, AC-54). Devuelve null si el paciente no tiene inscripción.</summary>
    Task<PatientOverviewDto?> GetPatientOverviewAsync(
        Guid patientId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Inscripción activa más reciente de un paciente: misma resolución que el
    /// overview ERP (status <c>Active</c>, orden <c>CreatedAt</c> desc).
    /// Devuelve null si el paciente no tiene inscripción activa. Base del read
    /// model ERP de Controles (UC-004).
    /// </summary>
    Task<ProgramEnrollment?> GetActiveEnrollmentForPatientAsync(
        Guid patientId,
        CancellationToken ct = default
    );

    // --- Bitácora de actividad (ERP) ---

    /// <summary>
    /// Bitácora de actividad del módulo (ERP): entradas del log de auditoría
    /// trigger-based (<c>audit.activity_logs</c>) filtradas a las tablas
    /// <c>app.*</c> del módulo, paginadas (orden descendente por
    /// <c>occurred_at</c>) con filtros opcionales por tabla, acción
    /// (INSERT/UPDATE/DELETE), ventana de fechas y actor (email contiene).
    /// Solo lectura; nunca expone <c>old_data</c>/<c>new_data</c>.
    /// </summary>
    Task<PaginatedActivityLogResult> ListActivityLogAsync(
        int page,
        int pageSize,
        string? tableName,
        string? action,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? actor,
        CancellationToken ct = default
    );

    // --- Biometría (SPEC §06) ---

    /// <summary>Resumen comunitario de biometría: promedios, distribuciones, evolución semanal, ciudades y alertas.</summary>
    Task<BiometriaCommunityDto> GetBiometriaCommunityAsync(CancellationToken ct = default);

    /// <summary>Información básica del paciente (PatientId, Gender, CityId) para métricas biométricas.</summary>
    Task<PatientBiometriaInfoDto?> GetPatientBiometriaInfoAsync(
        Guid enrollmentId,
        CancellationToken ct = default
    );

    /// <summary>Listado paginado de pacientes con indicadores de biometría (última medición).</summary>
    Task<(IReadOnlyList<BiometriaPatientListItemDto> Items, int Total)> ListBiometriaPatientsAsync(
        string? search,
        string? gender,
        string? imcCategory,
        string? glucosaCategory,
        string? grasaCategory,
        string? trend,
        Guid? cityId,
        string? stateAbbr,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    /// <summary>Detalle de biometría de un paciente: historial semanal, heatmap y datos exactos.</summary>
    Task<BiometriaPatientDetailDto?> GetBiometriaPatientAsync(
        Guid patientId,
        CancellationToken ct = default
    );

    /// <summary>Exporte CSV de biometría de pacientes.</summary>
    IAsyncEnumerable<BiometriaPatientListItemDto> StreamBiometriaPatientsForExportAsync(
        CancellationToken ct = default
    );

    // --- Exporte CSV (B14) ---

    /// <summary>
    /// Stream de inscripciones para el exporte CSV (B14, T-30): proyección
    /// <c>AsNoTracking</c> con <c>AsAsyncEnumerable</c> — NUNCA bufferiza el
    /// resultado completo. Filtros por clínica del paciente y ventana de
    /// creación; <paramref name="scopedPatientIds"/> aplica el scoping del actor
    /// (T-81): lista con ids = solo esas inscripciones; <c>null</c> = sin
    /// filtro; lista vacía = stream sin filas (denegado).
    /// </summary>
    IAsyncEnumerable<EnrollmentExportRow> StreamEnrollmentsForExportAsync(
        Guid? clinicId,
        DateTime? from,
        DateTime? to,
        IReadOnlyList<Guid>? scopedPatientIds,
        CancellationToken ct = default
    );

    // --- Reconciliación de rachas (B12) ---

    /// <summary>
    /// Reconciliación de rachas (B12, T-28): recalcula
    /// <c>current_streak</c>/<c>longest_streak</c>/<c>last_active_date</c> de
    /// CADA inscripción activa desde la fuente de verdad
    /// (<c>task_completions</c> por día ≥ umbral de la plantilla + días
    /// rescatados con congelamiento consumido) y corrige con
    /// <c>ExecuteUpdate</c> las filas desviadas (log de discrepancias).
    /// Idempotente: re-correr no vuelve a corregir. NUNCA toca
    /// <c>daily_checkins.is_perfect_day</c> ni el inventario de congelamientos.
    /// </summary>
    Task<StreakReconciliationSummary> ReconcileStreaksAsync(CancellationToken ct = default);

    // --- Catálogo clínico (línea base, ERP) ---

    /// <summary>
    /// Métricas clínicas activas del catálogo (<c>app.measurement_metrics</c>)
    /// con su unidad por defecto resuelta (<c>app.unit_of_measures</c>).
    /// Alimenta el selector de la línea base del ERP (GET
    /// /catalogs/clinical-metrics): el POST /enrollments/{id}/baselines valida
    /// metricId/unitId contra estas filas reales.
    /// </summary>
    Task<IReadOnlyList<MeasurementMetric>> ListClinicalMetricsAsync(CancellationToken ct = default);

    // --- Libro mayor de XP (TASK-04, ERP) ---

    /// <summary>
    /// Página del libro mayor de XP de una inscripción (TASK-04): entradas de
    /// <c>app.xp_ledger_entries</c> ordenadas descendente por <c>AwardedAt</c>
    /// (append-only). Devuelve las entradas de la página y el total de la
    /// inscripción para calcular <c>totalPages</c>.
    /// </summary>
    Task<(IReadOnlyList<XpLedgerEntry> Entries, int Total)> GetXpLedgerPageAsync(
        Guid enrollmentId,
        int page,
        int pageSize,
        CancellationToken ct = default
    );
}
