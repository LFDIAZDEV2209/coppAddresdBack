using CoppAddresd.Api.Authorization;
using CoppAddresd.Api.Context;
using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.ProgramProgress.Commands.ArchiveTemplate;
using CoppAddresd.Application.Features.ProgramProgress.Commands.CalculateScores;
using CoppAddresd.Application.Features.ProgramProgress.Commands.CompleteTask;
using CoppAddresd.Application.Features.ProgramProgress.Commands.CreateTemplate;
using CoppAddresd.Application.Features.ProgramProgress.Commands.DecideAdaptation;
using CoppAddresd.Application.Features.ProgramProgress.Commands.DecideClinicalReview;
using CoppAddresd.Application.Features.ProgramProgress.Commands.EnrollPatient;
using CoppAddresd.Application.Features.ProgramProgress.Commands.LogNutrition;
using CoppAddresd.Application.Features.ProgramProgress.Commands.PauseEnrollment;
using CoppAddresd.Application.Features.ProgramProgress.Commands.PublishTemplate;
using CoppAddresd.Application.Features.ProgramProgress.Commands.ReplaceWeekdayTasks;
using CoppAddresd.Application.Features.ProgramProgress.Commands.ResumeEnrollment;
using CoppAddresd.Application.Features.ProgramProgress.Commands.UpdateTemplate;
using CoppAddresd.Application.Features.ProgramProgress.Commands.UpdateXpRule;
using CoppAddresd.Application.Features.ProgramProgress.Commands.WithdrawEnrollment;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.Scores;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.ClinicalXp;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.Nutrition;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetAdaptation;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetCalendar;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetPath;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetScores;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetSnapshot;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetTemplate;
using CoppAddresd.Application.Features.ProgramProgress.Queries.ListAdaptations;
using CoppAddresd.Application.Features.ProgramProgress.Queries.ListClinicalReviews;
using CoppAddresd.Application.Features.ProgramProgress.Queries.ListEnrollments;
using CoppAddresd.Application.Features.ProgramProgress.Queries.ListTemplates;
using CoppAddresd.Application.Features.ProgramProgress.Queries.ListXpRules;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums.ProgramProgress;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

/// <summary>
/// Endpoints del módulo Progreso del Programa (SPEC §7), base
/// <c>/api/v1/program</c>. <c>[Authorize]</c> a nivel de controlador; cada
/// acción declara su permiso con <c>[RequirePermission("Program.X")]</c>
/// (códigos sembrados en <c>auth.permissions</c>).
///
/// Endpoints de paciente (snapshot, completar, calendario, sendero, auto
/// inscripción): el actor se resuelve SIEMPRE de la identidad del JWT vía
/// <see cref="IProgramActorContext"/> — nunca del body. Cualquier cruce entre
/// pacientes devuelve 404 (anti-IDOR, SPEC §6.14 + AC-11), nunca 403.
/// Endpoints de clínico/ERP: requieren el permiso explícito.
/// </summary>
[ApiController]
[Route("api/v1/program")]
[Authorize]
public sealed class ProgramController(
    IMediator mediator,
    ILogger<ProgramController> logger,
    IConfiguration configuration,
    IProgramActorContext actorContext,
    IObjectStorageService objectStorage) : ControllerBase
{
    private const string DefaultTemplateCodeKey = "Program:DefaultTemplate:Code";
    private const string DefaultTemplateCodeFallback = "default-83w";

    // ===================== PACIENTE: snapshot =====================

    /// <summary>
    /// Snapshot del programa para la home del móvil (SPEC §7.1). La inscripción
    /// es la activa del paciente autenticado (la ruta no recibe enrollmentId).
    /// El <c>thumbnailUrl</c> de cada tarea podcast se resuelve a la forma final
    /// de URL (proxy local o presign S3) en la capa API (B5).
    /// </summary>
    [HttpGet("me/snapshot")]
    public async Task<ActionResult<ProgramSnapshotDto>> GetMySnapshot(CancellationToken ct)
    {
        var enrollmentId = await actorContext.ResolveActiveEnrollmentIdAsync(ct);
        if (enrollmentId is null)
        {
            return NotFound(new { message = "NO_ACTIVE_ENROLLMENT: no existe una inscripción activa para el paciente." });
        }

        var snapshot = await mediator.Send(new GetSnapshotQuery(enrollmentId.Value), ct);
        return Ok(await ResolveThumbnailUrlsAsync(snapshot, ct));
    }

    // ===================== PACIENTE: completar tarea =====================

    /// <summary>
    /// Completa una tarea del programa (SPEC §7.2). El <c>enrollmentId</c> del
    /// body se valida contra el paciente autenticado (anti-IDOR AC-11): si no
    /// le pertenece → 404, sin distinguir si la inscripción existe (no filtra
    /// existencia). Replay idempotente → 200 con el body existente.
    /// </summary>
    [HttpPost("tasks/complete")]
    public async Task<ActionResult<CompleteTaskResponseDto>> CompleteTask(
        [FromBody] CompleteTaskRequest request,
        CancellationToken ct)
    {
        if (!await actorContext.EnrollmentBelongsToCurrentPatientAsync(request.EnrollmentId, ct))
        {
            return NotFound(new { message = "Inscripción no encontrada" });
        }

        var result = await mediator.Send(new CompleteTaskCommand(
            request.EnrollmentId,
            request.LocalDate,
            request.TaskCode,
            request.ClientRequestId,
            request.ClientCompletedAt,
            request.MoodScore,
            request.Barriers,
            request.ContentFingerprint,
            ActorId: actorContext.UserId), ct);

        logger.LogInformation(
            "Program.CompleteTask: enrollment={EnrollmentId} fecha={LocalDate} tarea={TaskCode} actor={ActorId}",
            request.EnrollmentId, request.LocalDate, request.TaskCode, actorContext.UserId);

        return Ok(result);
    }

    // ===================== PACIENTE: calendario =====================

    /// <summary>
    /// Rollups diarios de la ventana [from, to] (SPEC §7.3, máx 92 días). La
    /// inscripción es la activa del paciente autenticado.
    /// </summary>
    [HttpGet("calendar")]
    public async Task<ActionResult<ProgramCalendarDto>> GetCalendar(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct)
    {
        var enrollmentId = await actorContext.ResolveActiveEnrollmentIdAsync(ct);
        if (enrollmentId is null)
        {
            return NotFound(new { message = "NO_ACTIVE_ENROLLMENT: no existe una inscripción activa para el paciente." });
        }

        return Ok(await mediator.Send(new GetCalendarQuery(enrollmentId.Value, from, to), ct));
    }

    // ===================== PACIENTE: sendero =====================

    /// <summary>
    /// Sendero completo de semanas (SPEC §7.4). La inscripción es la activa del
    /// paciente autenticado.
    /// </summary>
    [HttpGet("path")]
    public async Task<ActionResult<ProgramPathDto>> GetPath(CancellationToken ct)
    {
        var enrollmentId = await actorContext.ResolveActiveEnrollmentIdAsync(ct);
        if (enrollmentId is null)
        {
            return NotFound(new { message = "NO_ACTIVE_ENROLLMENT: no existe una inscripción activa para el paciente." });
        }

        return Ok(await mediator.Send(new GetPathQuery(enrollmentId.Value), ct));
    }

    // ===================== PACIENTE: auto inscripción =====================

    /// <summary>
    /// Auto inscripción del paciente autenticado (móvil). El <c>patientId</c>
    /// se resuelve del JWT (nunca del body): el perfil de paciente del usuario
    /// es la identidad (SPEC §6.14). Sin perfil → 404; ya inscrito activo → 409
    /// (índice único parcial).
    /// </summary>
    [HttpPost("enrollments/me")]
    public async Task<ActionResult<ProgramEnrollmentDto>> EnrollSelf(
        [FromBody] EnrollRequest request,
        CancellationToken ct)
    {
        var patientId = await actorContext.ResolvePatientProfileIdAsync(ct);
        if (patientId is null)
        {
            return NotFound(new { message = "No existe un perfil de paciente para el usuario autenticado." });
        }

        var result = await mediator.Send(new EnrollPatientCommand(
            patientId.Value,
            request.TemplateId,
            request.Timezone,
            request.StartLocalDate,
            DefaultTemplateCode: DefaultTemplateCode(),
            ActorId: actorContext.UserId), ct);

        return Ok(result);
    }

    // ===================== CLÍNICO/ERP: inscripciones =====================

    /// <summary>
    /// Inscripción de un paciente por un clínico (SPEC §7.5): el
    /// <c>patientId</c> del body es la selección del clínico (permiso
    /// <c>Program.Enroll</c>).
    /// </summary>
    [HttpPost("enrollments")]
    [RequirePermission("Program.Enroll")]
    public async Task<ActionResult<ProgramEnrollmentDto>> EnrollPatient(
        [FromBody] EnrollRequest request,
        CancellationToken ct)
    {
        if (request.PatientId is null || request.PatientId == Guid.Empty)
        {
            return BadRequest(new { message = "El patientId es requerido." });
        }

        var result = await mediator.Send(new EnrollPatientCommand(
            request.PatientId.Value,
            request.TemplateId,
            request.Timezone,
            request.StartLocalDate,
            DefaultTemplateCode: DefaultTemplateCode(),
            ActorId: actorContext.UserId), ct);

        return Ok(result);
    }

    /// <summary>Pausa una inscripción activa (Active → Paused, SPEC §7.5).</summary>
    [HttpPost("enrollments/{id:guid}/pause")]
    [RequirePermission("Program.Enroll")]
    public async Task<ActionResult<ProgramEnrollmentDto>> PauseEnrollment(
        Guid id,
        [FromBody] EnrollmentActionRequest? request,
        CancellationToken ct)
        => Ok(await mediator.Send(new PauseEnrollmentCommand(id, request?.Reason, actorContext.UserId), ct));

    /// <summary>Reanuda una inscripción pausada (Paused → Active, SPEC §7.5).</summary>
    [HttpPost("enrollments/{id:guid}/resume")]
    [RequirePermission("Program.Enroll")]
    public async Task<ActionResult<ProgramEnrollmentDto>> ResumeEnrollment(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new ResumeEnrollmentCommand(id, actorContext.UserId), ct));

    /// <summary>Retira una inscripción (terminal, conserva historial, SPEC §7.5).</summary>
    [HttpPost("enrollments/{id:guid}/withdraw")]
    [RequirePermission("Program.Enroll")]
    public async Task<ActionResult<ProgramEnrollmentDto>> WithdrawEnrollment(
        Guid id,
        [FromBody] EnrollmentActionRequest? request,
        CancellationToken ct)
        => Ok(await mediator.Send(new WithdrawEnrollmentCommand(id, request?.Reason, actorContext.UserId), ct));

    /// <summary>Listado paginado de inscripciones con filtros (SPEC §7.5, ERP).</summary>
    [HttpGet("enrollments")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<PaginatedEnrollmentsResult>> ListEnrollments(
        [FromQuery] Guid? patientId,
        [FromQuery] ProgramEnrollmentStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await mediator.Send(new ListEnrollmentsQuery(patientId, status, page, pageSize), ct));

    // ===================== CLÍNICO/ERP: plantillas =====================

    /// <summary>Listado paginado de plantillas (SPEC §7.6, ERP).</summary>
    [HttpGet("templates")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<PaginatedTemplatesResult>> ListTemplates(
        [FromQuery] string? search,
        [FromQuery] TemplateStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await mediator.Send(new ListTemplatesQuery(search, status, page, pageSize), ct));

    /// <summary>Detalle de una plantilla con sus filas por día (SPEC §7.6).</summary>
    [HttpGet("templates/{id:guid}")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<ProgramTemplateDto>> GetTemplate(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetTemplateQuery(id), ct));

    /// <summary>Crea una plantilla en estado Draft (SPEC §7.6).</summary>
    [HttpPost("templates")]
    [RequirePermission("Program.Edit")]
    public async Task<ActionResult<ProgramTemplateDto>> CreateTemplate(
        [FromBody] CreateTemplateRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(new CreateTemplateCommand(
            request.Code,
            request.Name,
            request.Description,
            request.TotalWeeks,
            request.Days,
            actorContext.UserId), ct);

        return CreatedAtAction(nameof(GetTemplate), new { id = result.Id }, result);
    }

    /// <summary>Actualiza una plantilla existente (SPEC §7.6).</summary>
    [HttpPut("templates/{id:guid}")]
    [RequirePermission("Program.Edit")]
    public async Task<ActionResult<ProgramTemplateDto>> UpdateTemplate(
        Guid id,
        [FromBody] UpdateTemplateRequest request,
        CancellationToken ct)
        => Ok(await mediator.Send(new UpdateTemplateCommand(
            id, request.Code, request.Name, request.Description, request.TotalWeeks,
            request.Days, actorContext.UserId), ct));

    /// <summary>Publica una plantilla: bump de versión y Draft → Active (SPEC §7.6).</summary>
    [HttpPost("templates/{id:guid}/publish")]
    [RequirePermission("Program.Edit")]
    public async Task<ActionResult<ProgramTemplateDto>> PublishTemplate(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new PublishTemplateCommand(id, actorContext.UserId), ct));

    /// <summary>Archiva una plantilla (idempotente, conserva historia, SPEC §7.6).</summary>
    [HttpPost("templates/{id:guid}/archive")]
    [RequirePermission("Program.Edit")]
    public async Task<ActionResult<ProgramTemplateDto>> ArchiveTemplate(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new ArchiveTemplateCommand(id, actorContext.UserId), ct));

    /// <summary>
    /// Lista las filas por día de una plantilla (SPEC §7.6). Es la misma forma
    /// <c>WeeklyDayTemplateDto</c> del detalle de la plantilla.
    /// </summary>
    [HttpGet("templates/{id:guid}/weekday-tasks")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<IReadOnlyList<WeeklyDayTemplateDto>>> GetWeekdayTasks(
        Guid id, CancellationToken ct)
    {
        var template = await mediator.Send(new GetTemplateQuery(id), ct);
        return Ok(template.Days);
    }

    /// <summary>Reemplazo en bloque del horario semanal (SPEC §7.6).</summary>
    [HttpPut("templates/{id:guid}/weekday-tasks")]
    [RequirePermission("Program.Edit")]
    public async Task<ActionResult<IReadOnlyList<WeeklyDayTemplateDto>>> ReplaceWeekdayTasks(
        Guid id,
        [FromBody] IReadOnlyList<WeeklyDayTemplateRequest> tasks,
        CancellationToken ct)
        => Ok(await mediator.Send(new ReplaceWeekdayTasksCommand(id, tasks, actorContext.UserId), ct));

    // ===================== CLÍNICO/ERP: adaptaciones =====================

    /// <summary>Listado paginado de recomendaciones de adaptación (SPEC §7.7).</summary>
    [HttpGet("adaptations")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<PaginatedAdaptationsResult>> ListAdaptations(
        [FromQuery] Guid? enrollmentId,
        [FromQuery] AdaptationStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await mediator.Send(new ListAdaptationsQuery(enrollmentId, status, page, pageSize), ct));

    /// <summary>Detalle de una recomendación de adaptación (SPEC §7.7).</summary>
    [HttpGet("adaptations/{id:guid}")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<AdaptationRecommendationDto>> GetAdaptation(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetAdaptationQuery(id), ct));

    /// <summary>Aprueba o rechaza una recomendación de adaptación (SPEC §7.7).</summary>
    [HttpPost("adaptations/{id:guid}/decide")]
    [RequirePermission("Program.Adapt")]
    public async Task<ActionResult<AdaptationRecommendationDto>> DecideAdaptation(
        Guid id,
        [FromBody] DecideAdaptationRequest request,
        CancellationToken ct)
        => Ok(await mediator.Send(new DecideAdaptationCommand(id, request.Decision, request.Note, actorContext.UserId), ct));

    // ===================== PUNTUACIONES (SPEC §13) =====================

    /// <summary>
    /// Índice de Salud + Índice de Transformación del paciente autenticado
    /// (SPEC §13.7.1, pestaña Evolución del móvil). Compute-on-read (SPEC
    /// §13.3): si la fila del período falta o está vencida, el repositorio
    /// recalcula y persiste; la respuesta trae <c>current</c> + <c>previous</c>
    /// para la tendencia. El <c>patientId</c> se resuelve del JWT vía
    /// <see cref="IProgramActorContext"/> (nunca del body): sin perfil de
    /// paciente o sin inscripción activa → 404 (anti-IDOR AC-11).
    /// </summary>
    [HttpGet("scores")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<ScoresResponseDto>> GetScores(CancellationToken ct)
    {
        var patientId = await actorContext.ResolvePatientProfileIdAsync(ct);
        if (patientId is null)
        {
            return NotFound(new { message = "No existe un perfil de paciente para el usuario autenticado." });
        }

        return Ok(await mediator.Send(new GetScoresQuery(patientId.Value), ct));
    }

    /// <summary>
    /// Recálculo forzado de puntajes por un clínico (SPEC §13.7.2): único
    /// disparador fuera de banda del motor (sin cron en MVP, §13.3). Body
    /// <c>{ patientId, periodEndLocalDate? }</c>; el fin de período opcional
    /// aplica al Índice de Salud (una fecha futura → 422 INVALID_PERIOD).
    /// Devuelve el mismo shape que <c>GET /scores</c> con el header
    /// <c>X-Score-Recalculated: true</c> para distinguir el cómputo fresco de
    /// un cache hit. Paciente sin inscripción activa → 404 (el clínico nunca
    /// distingue si el paciente existe).
    /// </summary>
    [HttpPost("scores/calculate")]
    [RequirePermission("Program.Edit")]
    public async Task<ActionResult<ScoresResponseDto>> CalculateScores(
        [FromBody] CalculateScoresRequest request,
        CancellationToken ct)
    {
        if (request.PatientId == Guid.Empty)
        {
            return BadRequest(new { message = "El patientId es requerido." });
        }

        Response.Headers["X-Score-Recalculated"] = "true";

        return Ok(await mediator.Send(
            new CalculateScoresCommand(request.PatientId, request.PeriodEndLocalDate), ct));
    }

    // ===================== CLÍNICO/ERP: catálogo de reglas XP (SPEC §14) =====================

    /// <summary>
    /// Catálogo completo de reglas XP (SPEC §14.5, ERP): código, puntos base,
    /// multiplicador, topes anti-fraude y vigencia de cada regla. Catálogo
    /// pequeño y estable (11 reglas sembradas): se devuelve completo, sin
    /// paginación. Requiere <c>Program.Edit</c> (los valores influyen en cómo
    /// se otorga XP a los pacientes).
    /// </summary>
    [HttpGet("xp-rules")]
    [RequirePermission("Program.Edit")]
    public async Task<ActionResult<IReadOnlyList<XpRuleDto>>> ListXpRules(CancellationToken ct)
        => Ok(await mediator.Send(new ListXpRulesQuery(), ct));

    /// <summary>
    /// Actualiza una regla del catálogo de XP (SPEC §14.4, ERP, prospective
    /// only): base_xp, multiplier, max_per_day, max_per_week,
    /// requires_validation, active y valid_until (nullable). Nunca reescribe
    /// el historial de <c>app.xp_ledger</c>; solo afecta otorgamientos futuros.
    /// La identidad de la regla (código, nombre, categoría, valid_from) no es
    /// editable por esta vía.
    /// </summary>
    [HttpPut("xp-rules/{code}")]
    [RequirePermission("Program.Edit")]
    public async Task<ActionResult<XpRuleDto>> UpdateXpRule(
        string code,
        [FromBody] UpdateXpRuleRequest request,
        CancellationToken ct)
        => Ok(await mediator.Send(new UpdateXpRuleCommand(
            code,
            request.BaseXp,
            request.Multiplier,
            request.MaxPerDay,
            request.MaxPerWeek,
            request.RequiresValidation,
            request.Active,
            request.ValidUntil,
            actorContext.UserId), ct));

    // ============ CLÍNICO: revisiones clínicas de XP (SPEC §15, D) ============

    /// <summary>
    /// Cola de revisiones clínicas de XP pendientes (SPEC §15, D): mejorías
    /// significativas detectadas por <c>POST /scores/calculate</c> que esperan
    /// decisión (<c>status = 'pending'</c>). Cada fila trae paciente, métrica
    /// (código/nombre), |Δ%| y el período del Índice de Salud. Requiere
    /// <c>Program.Adapt</c> (decisión clínica del módulo).
    /// </summary>
    [HttpGet("xp-rules/clinical-pending")]
    [RequirePermission("Program.Adapt")]
    public async Task<ActionResult<PaginatedClinicalReviewsResult>> ListClinicalReviews(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await mediator.Send(new ListClinicalReviewsQuery(page, pageSize), ct));

    /// <summary>
    /// Decide una revisión clínica de XP (SPEC §15, D): <c>{ approve: boolean }</c>.
    /// Aprobada → se otorga <c>CLINICAL_SIGNIFICANT</c> con
    /// <c>validated_by</c>/<c>validated_at</c> del clínico (cuenta en los
    /// totales de XP); rechazada → sin XP, nunca penaliza. Requiere
    /// <c>Program.Adapt</c> + rol clínico del actor (AC-22: un paciente
    /// decidiendo su propia XP significativa → 403). Ya decidida → 409
    /// <c>REVIEW_ALREADY_DECIDED</c>.
    /// </summary>
    [HttpPost("xp-rules/clinical-pending/{id:guid}/decide")]
    [RequirePermission("Program.Adapt")]
    public async Task<ActionResult<ClinicalReviewDto>> DecideClinicalReview(
        Guid id,
        [FromBody] DecideClinicalReviewRequest request,
        CancellationToken ct)
        => Ok(await mediator.Send(new DecideClinicalReviewCommand(
            id, request.Approve, actorContext.UserId, actorContext.Roles), ct));

    // ============ PACIENTE: nutrición granular (SPEC §18, B) ============

    /// <summary>
    /// Registra una comida o hidratación del paciente autenticado (SPEC §18, B):
    /// cierra el bucle de la NutritionPage del móvil (4 comidas
    /// <c>des</c>/<c>alm</c>/<c>mer</c>/<c>cen</c> + hidratación <c>agua</c>).
    /// Body <c>{ mealCode, localDate? }</c>; el <c>localDate</c> opcional se
    /// resuelve contra el hoy local del paciente (una fecha futura → 422
    /// <c>INVALID_DATE</c>). Crea el <c>app.habit_checks</c> idempotente y
    /// otorga la XP granular por el camino del catálogo
    /// (<c>NUTRITION_MEAL_COMPLETE</c> 10/día ×4, <c>NUTRITION_HYDRATION</c>
    /// 5/día ×1) — ADITIVO a la tarea <c>nut</c> existente (la tarea del
    /// programa no se auto-completa aquí). Un log duplicado → 409
    /// <c>HABIT_ALREADY_LOGGED</c>; la XP nunca se duplica.
    /// El <c>patientId</c> se resuelve del JWT (nunca del body): sin perfil de
    /// paciente → 404 (anti-IDOR AC-11).
    /// </summary>
    [HttpPost("nutrition/log")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<NutritionLogResultDto>> LogNutrition(
        [FromBody] LogNutritionRequest request,
        CancellationToken ct)
    {
        var patientId = await actorContext.ResolvePatientProfileIdAsync(ct);
        if (patientId is null)
        {
            return NotFound(new { message = "No existe un perfil de paciente para el usuario autenticado." });
        }

        var result = await mediator.Send(new LogNutritionCommand(
            patientId.Value, request.MealCode, request.LocalDate, actorContext.UserId), ct);

        logger.LogInformation(
            "Program.NutritionLog: meal={MealCode} fecha={LocalDate} xpAwarded={XpAwarded}",
            result.MealCode, result.LocalDate, result.XpAwarded);

        return Ok(result);
    }

    // ===================== helpers =====================

    /// <summary>
    /// Resuelve el <c>thumbnailUrl</c> de cada tarea del snapshot a su forma
    /// final de URL (SPEC §7.1 + T-13): con el proveedor S3 un presign real; con
    /// el proveedor Local el proxy del backend <c>{host}/api/v1/storage/{key}</c>
    /// (misma convención que <c>MediaController</c>/<c>StorageController</c>).
    /// </summary>
    private async Task<ProgramSnapshotDto> ResolveThumbnailUrlsAsync(
        ProgramSnapshotDto snapshot, CancellationToken ct)
    {
        if (snapshot.TodayTasks.All(t => t.Content?.ThumbnailUrl is null))
        {
            return snapshot;
        }

        var tasks = new List<TodayTaskDto>(snapshot.TodayTasks.Count);
        foreach (var task in snapshot.TodayTasks)
        {
            if (task.Content?.ThumbnailUrl is not { } thumbnailKey)
            {
                tasks.Add(task);
                continue;
            }

            var resolvedUrl = await ResolveMediaUrlAsync(thumbnailKey, ct);
            tasks.Add(task with
            {
                Content = new TodayTaskContentDto(
                    task.Content.MediaId, task.Content.Title, task.Content.DurationSecs, resolvedUrl),
            });
        }

        return snapshot with { TodayTasks = tasks };
    }

    /// <summary>Forma final de URL de un objeto de storage (key → URL).</summary>
    private async Task<string?> ResolveMediaUrlAsync(string? key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        // Ya es una URL absoluta (precedente: media ya resueltos por el repo).
        if (key.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return key;
        }

        if (objectStorage.IsCloudStorage)
        {
            return await objectStorage.GetPreSignedUrlAsync(
                key, TimeSpan.FromHours(1), ct);
        }

        return $"{Request.Scheme}://{Request.Host}/api/v1/storage/{key}";
    }

    /// <summary>Código de la plantilla por defecto (config, fallback <c>default-83w</c>).</summary>
    private string DefaultTemplateCode()
        => configuration[DefaultTemplateCodeKey] ?? DefaultTemplateCodeFallback;
}

/// <summary>Payload de <c>POST /api/v1/program/tasks/complete</c> (SPEC §7.2).</summary>
public sealed record CompleteTaskRequest(
    Guid EnrollmentId,
    DateOnly LocalDate,
    TaskCode TaskCode,
    string? ClientRequestId,
    DateTime? ClientCompletedAt,
    short? MoodScore,
    string? Barriers,
    string? ContentFingerprint);

/// <summary>
/// Payload de inscripción (<c>POST /enrollments</c> clínico y
/// <c>POST /enrollments/me</c> paciente, SPEC §7.5). En el flujo del paciente el
/// <c>patientId</c> se ignora: la identidad sale del JWT (SPEC §6.14).
/// </summary>
public sealed record EnrollRequest(
    Guid? PatientId,
    Guid? TemplateId,
    string Timezone,
    DateOnly? StartLocalDate);

/// <summary>Body de pause/withdraw: motivo informativo (no persiste en MVP, se loguea).</summary>
public sealed record EnrollmentActionRequest(string? Reason);

/// <summary>Payload de creación de plantilla (SPEC §7.6).</summary>
public sealed record CreateTemplateRequest(
    string Code,
    string Name,
    string? Description,
    int TotalWeeks,
    IReadOnlyList<WeeklyDayTemplateRequest> Days);

/// <summary>Payload de actualización de plantilla (SPEC §7.6).</summary>
public sealed record UpdateTemplateRequest(
    string Code,
    string Name,
    string? Description,
    int TotalWeeks,
    IReadOnlyList<WeeklyDayTemplateRequest> Days);

/// <summary>Payload de <c>POST /adaptations/{id}/decide</c> (SPEC §7.7).</summary>
public sealed record DecideAdaptationRequest(AdaptationDecisionAction Decision, string? Note);

/// <summary>
/// Payload de <c>POST /api/v1/program/scores/calculate</c> (SPEC §13.7.2):
/// <c>patientId</c> es la selección del clínico (permiso <c>Program.Edit</c>)
/// y <c>periodEndLocalDate</c> opcional fija el fin del período del Índice de
/// Salud en fecha local del paciente.
/// </summary>
public sealed record CalculateScoresRequest(Guid PatientId, DateOnly? PeriodEndLocalDate);

/// <summary>
/// Payload de <c>PUT /api/v1/program/xp-rules/{code}</c> (SPEC §14.4, ERP):
/// campos editables de la regla. <b>Prospective only</b>: la edición nunca
/// reescribe el historial de <c>app.xp_ledger</c>. <c>validUntil</c> null
/// significa vigencia sin fecha límite; <c>baseXp</c> null significa "diferir a
/// la fuente de puntos existente" (puntos de plantilla para las <c>TASK_*</c>).
/// </summary>
public sealed record UpdateXpRuleRequest(
    int? BaseXp,
    decimal Multiplier,
    int? MaxPerDay,
    int? MaxPerWeek,
    bool RequiresValidation,
    bool Active,
    DateOnly? ValidUntil);

/// <summary>
/// Payload de <c>POST /api/v1/program/xp-rules/clinical-pending/{id}/decide</c>
/// (SPEC §15, D): <c>approve</c> aprobada otorga <c>CLINICAL_SIGNIFICANT</c>
/// validada por el clínico; <c>false</c> la rechaza sin XP.
/// </summary>
public sealed record DecideClinicalReviewRequest(bool Approve);

/// <summary>
/// Payload de <c>POST /api/v1/program/nutrition/log</c> (SPEC §18, B):
/// <c>mealCode</c> es la comida/hidratación del móvil
/// (<c>des</c>/<c>alm</c>/<c>mer</c>/<c>cen</c>/<c>agua</c>) y
/// <c>localDate</c> opcional es la fecha local del paciente (por defecto, el
/// hoy local; una fecha futura → 422 <c>INVALID_DATE</c>).
/// </summary>
public sealed record LogNutritionRequest(MealCode MealCode, DateOnly? LocalDate);