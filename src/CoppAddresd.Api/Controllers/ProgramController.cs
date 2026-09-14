using System.Globalization;
using CoppAddresd.Application.Features.ProgramProgress.Commands.RecordWeight;
using CoppAddresd.Api.Authorization;
using CoppAddresd.Api.Constants;
using CoppAddresd.Api.Context;
using CoppAddresd.Api.Security;
using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.ProgramProgress.Commands.AcceptIntervention;
using CoppAddresd.Application.Features.ProgramProgress.Commands.ArchiveTemplate;
using CoppAddresd.Application.Features.ProgramProgress.Commands.BulkEnrollPatients;
using CoppAddresd.Application.Features.ProgramProgress.Commands.CalculateScores;
using CoppAddresd.Application.Features.ProgramProgress.Commands.CompleteTask;
using CoppAddresd.Application.Features.ProgramProgress.Commands.CreateBaseline;
using CoppAddresd.Application.Features.ProgramProgress.Commands.CreateTemplate;
using CoppAddresd.Application.Features.ProgramProgress.Commands.DecideAdaptation;
using CoppAddresd.Application.Features.ProgramProgress.Commands.DecideClinicalReview;
using CoppAddresd.Application.Features.ProgramProgress.Commands.EnrollPatient;
using CoppAddresd.Application.Features.ProgramProgress.Commands.LogNutrition;
using CoppAddresd.Application.Features.ProgramProgress.Commands.MarkNotificationRead;
using CoppAddresd.Application.Features.ProgramProgress.Commands.MarkTeleAttended;
using CoppAddresd.Application.Features.ProgramProgress.Commands.MarkTeleComply;
using CoppAddresd.Application.Features.ProgramProgress.Commands.MarkTeleScheduled;
using CoppAddresd.Application.Features.ProgramProgress.Commands.PauseEnrollment;
using CoppAddresd.Application.Features.ProgramProgress.Commands.PublishTemplate;
using CoppAddresd.Application.Features.ProgramProgress.Commands.ReconcileStreaks;
using CoppAddresd.Application.Features.ProgramProgress.Commands.ReplaceEnrollmentWeekTasks;
using CoppAddresd.Application.Features.ProgramProgress.Commands.ReplaceWeekdayTasks;
using CoppAddresd.Application.Features.ProgramProgress.Commands.ResumeEnrollment;
using CoppAddresd.Application.Features.ProgramProgress.Commands.SetWeekContent;
using CoppAddresd.Application.Features.ProgramProgress.Commands.SetWeekContentRange;
using CoppAddresd.Application.Features.ProgramProgress.Commands.UpdateInterventionStatus;
using CoppAddresd.Application.Features.ProgramProgress.Commands.UpdateLeaguePreferences;
using CoppAddresd.Application.Features.ProgramProgress.Commands.UpdateTemplate;
using CoppAddresd.Application.Features.ProgramProgress.Commands.UpdateWeaknessStatus;
using CoppAddresd.Application.Features.ProgramProgress.Commands.UpdateXpRule;
using CoppAddresd.Application.Features.ProgramProgress.Commands.WithdrawEnrollment;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.ActivityLog;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.ClinicalXp;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.Erp;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.Interventions;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.League;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.MetricsHistory;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.Notifications;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.Nutrition;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.Scores;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.Weaknesses;
using CoppAddresd.Application.Features.ProgramProgress.Queries.Erp;
using CoppAddresd.Application.Features.ProgramProgress.Queries.ExportEnrollments;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetAdaptation;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetBaselines;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetCalendar;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetEnrollment;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetEnrollmentWeek;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetLeague;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetMetricsHistory;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetPath;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetProgramContent;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetScores;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetScoresHistory;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetSnapshot;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetTemplate;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetXpLedger;
using CoppAddresd.Application.Features.ProgramProgress.Queries.ListAdaptations;
using CoppAddresd.Application.Features.ProgramProgress.Queries.ListClinicalReviews;
using CoppAddresd.Application.Features.ProgramProgress.Queries.ListEnrollments;
using CoppAddresd.Application.Features.ProgramProgress.Queries.ListInterventions;
using CoppAddresd.Application.Features.ProgramProgress.Queries.ListNotifications;
using CoppAddresd.Application.Features.ProgramProgress.Queries.ListOpenInterventions;
using CoppAddresd.Application.Features.ProgramProgress.Queries.ListOpenWeaknesses;
using CoppAddresd.Application.Features.ProgramProgress.Queries.ListProgramActivityLog;
using CoppAddresd.Application.Features.ProgramProgress.Queries.ListTemplates;
using CoppAddresd.Application.Features.ProgramProgress.Queries.ListWeaknesses;
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
    IObjectStorageService objectStorage,
    StorageSignatureService? signatureService = null
) : ControllerBase
{
    private const string DefaultTemplateCodeKey = "Program:DefaultTemplate:Code";
    private const string DefaultTemplateCodeFallback = "default-83w";

    // ===================== PACIENTE: snapshot =====================

    /// <summary>
    /// Snapshot del programa para la home del móvil (SPEC §7.1). La inscripción
    /// es la activa del paciente autenticado (la ruta no recibe enrollmentId).
    /// El <c>thumbnailUrl</c> y <c>mediaUrl</c> de cada tarea podcast se resuelven a la forma final
    /// de URL (proxy firmado local o presign S3) en la capa API (B5).
    /// </summary>
    [HttpGet("me/snapshot")]
    public async Task<ActionResult<ProgramSnapshotDto>> GetMySnapshot(CancellationToken ct)
    {
        var enrollmentId = await actorContext.ResolveActiveEnrollmentIdAsync(ct);
        if (enrollmentId is null)
        {
            return NotFound(
                new
                {
                    message = "NO_ACTIVE_ENROLLMENT: no existe una inscripción activa para el paciente.",
                }
            );
        }

        var snapshot = await mediator.Send(new GetSnapshotQuery(enrollmentId.Value), ct);
        return Ok(await ResolveMediaUrlsAsync(snapshot, ct));
    }

    /// <summary>
    /// Snapshot del programa de un paciente específico visto desde el ERP
    /// (TASK-05b): recibe el <c>enrollmentId</c> directo (el clínico elige qué
    /// paciente ver, sin pasar por la identidad del JWT). Requiere
    /// <c>Program.View</c>. Inscripción inexistente → 404 (el handler lanza
    /// <c>NotFoundException</c>, que el middleware global mapea a 404).
    /// </summary>
    [HttpGet("enrollments/{id:guid}/snapshot")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<ProgramSnapshotDto>> GetEnrollmentSnapshot(
        Guid id,
        CancellationToken ct
    ) => Ok(await ResolveMediaUrlsAsync(await mediator.Send(new GetSnapshotQuery(id), ct), ct));

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
        CancellationToken ct
    )
    {
        if (!await actorContext.EnrollmentBelongsToCurrentPatientAsync(request.EnrollmentId, ct))
        {
            return NotFound(new { message = "Inscripción no encontrada" });
        }

        var result = await mediator.Send(
            new CompleteTaskCommand(
                request.EnrollmentId,
                request.LocalDate,
                request.TaskCode,
                request.ClientRequestId,
                request.ClientCompletedAt,
                request.MoodScore,
                request.Barriers,
                request.ContentFingerprint,
                ActorId: actorContext.UserId,
                Vitals: request.Vitals
            ),
            ct
        );

        logger.LogInformation(
            "Program.CompleteTask: enrollment={EnrollmentId} fecha={LocalDate} tarea={TaskCode} actor={ActorId}",
            request.EnrollmentId,
            request.LocalDate,
            request.TaskCode,
            actorContext.UserId
        );

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
        CancellationToken ct
    )
    {
        var enrollmentId = await actorContext.ResolveActiveEnrollmentIdAsync(ct);
        if (enrollmentId is null)
        {
            return NotFound(
                new
                {
                    message = "NO_ACTIVE_ENROLLMENT: no existe una inscripción activa para el paciente.",
                }
            );
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
            return NotFound(
                new
                {
                    message = "NO_ACTIVE_ENROLLMENT: no existe una inscripción activa para el paciente.",
                }
            );
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
        CancellationToken ct
    )
    {
        var patientId = await actorContext.ResolvePatientProfileIdAsync(ct);
        if (patientId is null)
        {
            return NotFound(
                new { message = "No existe un perfil de paciente para el usuario autenticado." }
            );
        }

        var result = await mediator.Send(
            new EnrollPatientCommand(
                patientId.Value,
                request.TemplateId,
                request.Timezone,
                request.StartLocalDate,
                DefaultTemplateCode: DefaultTemplateCode(),
                ActorId: actorContext.UserId
            ),
            ct
        );

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
        CancellationToken ct
    )
    {
        if (request.PatientId is null || request.PatientId == Guid.Empty)
        {
            return BadRequest(new { message = "El patientId es requerido." });
        }

        var result = await mediator.Send(
            new EnrollPatientCommand(
                request.PatientId.Value,
                request.TemplateId,
                request.Timezone,
                request.StartLocalDate,
                DefaultTemplateCode: DefaultTemplateCode(),
                ActorId: actorContext.UserId
            ),
            ct
        );

        return Ok(result);
    }

    /// <summary>Pausa una inscripción activa (Active → Paused, SPEC §7.5).
    /// Scoping T-81: solo Admin/org-clinic admin, el dueño o un profesional
    /// asignado; otro actor → 404.</summary>
    [HttpPost("enrollments/{id:guid}/pause")]
    [RequirePermission("Program.Enroll")]
    public async Task<ActionResult<ProgramEnrollmentDto>> PauseEnrollment(
        Guid id,
        [FromBody] EnrollmentActionRequest? request,
        CancellationToken ct
    )
    {
        if (!await actorContext.ActorScopedToEnrollmentAsync(id, ct))
        {
            return NotFound(new { message = "Inscripción no encontrada" });
        }

        return Ok(
            await mediator.Send(
                new PauseEnrollmentCommand(id, request?.Reason, actorContext.UserId),
                ct
            )
        );
    }

    /// <summary>Reanuda una inscripción pausada (Paused → Active, SPEC §7.5).
    /// Scoping T-81: mismo alcance que pause.</summary>
    [HttpPost("enrollments/{id:guid}/resume")]
    [RequirePermission("Program.Enroll")]
    public async Task<ActionResult<ProgramEnrollmentDto>> ResumeEnrollment(
        Guid id,
        CancellationToken ct
    )
    {
        if (!await actorContext.ActorScopedToEnrollmentAsync(id, ct))
        {
            return NotFound(new { message = "Inscripción no encontrada" });
        }

        return Ok(await mediator.Send(new ResumeEnrollmentCommand(id, actorContext.UserId), ct));
    }

    /// <summary>Retira una inscripción (terminal, conserva historial, SPEC §7.5).
    /// Scoping T-81: mismo alcance que pause.</summary>
    [HttpPost("enrollments/{id:guid}/withdraw")]
    [RequirePermission("Program.Enroll")]
    public async Task<ActionResult<ProgramEnrollmentDto>> WithdrawEnrollment(
        Guid id,
        [FromBody] EnrollmentActionRequest? request,
        CancellationToken ct
    )
    {
        if (!await actorContext.ActorScopedToEnrollmentAsync(id, ct))
        {
            return NotFound(new { message = "Inscripción no encontrada" });
        }

        return Ok(
            await mediator.Send(
                new WithdrawEnrollmentCommand(id, request?.Reason, actorContext.UserId),
                ct
            )
        );
    }

    /// <summary>Listado paginado de inscripciones con filtros (SPEC §7.5, ERP).
    /// Scoping T-81: clínicos solo ven inscripciones de pacientes asignados;
    /// pacientes, la propia; Admin/org-clinic admin, todas. <paramref name="patientId"/>
    /// tolerante: texto que no parsea a GUID → resultado vacío (200), nunca 400
    /// de binding.</summary>
    [HttpGet("enrollments")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<PaginatedEnrollmentsResult>> ListEnrollments(
        [FromQuery] string? patientId,
        [FromQuery] ProgramEnrollmentStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    )
    {
        // Filtro por GUID opcional: texto no-GUID (búsqueda a medias del ERP)
        // → resultado vacío en vez de 400 de model binding.
        Guid? patientFilter = null;
        var hasPatientFilter = !string.IsNullOrWhiteSpace(patientId);
        if (hasPatientFilter)
        {
            patientFilter = Guid.TryParse(patientId!.Trim(), out var parsed) ? parsed : Guid.Empty;
        }

        var scopedPatientIds = await actorContext.ResolveScopedPatientIdsAsync(ct);

        if (hasPatientFilter && patientFilter == Guid.Empty)
        {
            return Ok(
                new PaginatedEnrollmentsResult(
                    [],
                    0,
                    Math.Max(1, page),
                    Math.Clamp(pageSize, 1, 100),
                    1
                )
            );
        }

        return Ok(
            await mediator.Send(
                new ListEnrollmentsQuery(patientFilter, status, page, pageSize, scopedPatientIds),
                ct
            )
        );
    }

    /// <summary>
    /// Inscripción masiva de pacientes (B13, T-29): despacha la inscripción
    /// individual por cada paciente (MISMO camino de <c>POST /enrollments</c>)
    /// y reporta el resultado por fila — un fallo individual nunca aborta el
    /// lote. Tope de 100 pacientes por request (lotes mayores → 400; el
    /// dispatcher asíncrono queda como trabajo futuro). Requiere
    /// <c>Program.Enroll</c>.
    /// </summary>
    [HttpPost("enrollments/bulk")]
    [RequirePermission(PermissionCodes.ProgramEnroll)]
    public async Task<ActionResult<BulkEnrollPatientsResultDto>> BulkEnroll(
        [FromBody] BulkEnrollRequest request,
        CancellationToken ct
    )
    {
        if (request.PatientIds is { Count: 0 })
        {
            return BadRequest(new { message = "La lista de patientIds es requerida." });
        }

        return Ok(
            await mediator.Send(
                new BulkEnrollPatientsCommand(
                    request.PatientIds,
                    request.TemplateId,
                    request.Timezone,
                    request.StartLocalDate,
                    DefaultTemplateCode(),
                    actorContext.UserId
                ),
                ct
            )
        );
    }

    /// <summary>
    /// Exporte CSV de inscripciones (B14, T-30): stream <c>text/csv</c> vía
    /// <c>IAsyncEnumerable</c> — sin bufferar el resultado completo. Filtros
    /// opcionales por clínica del paciente y ventana de creación; aplica el
    /// scoping del actor (T-81). Requiere <c>Program.Export</c> (sembrado P3).
    /// </summary>
    [HttpGet("enrollments/export")]
    [RequirePermission(PermissionCodes.ProgramExport)]
    public async Task<IActionResult> ExportEnrollments(
        [FromQuery] Guid? clinicId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct = default
    )
    {
        var scopedPatientIds = await actorContext.ResolveScopedPatientIdsAsync(ct);

        Response.ContentType = "text/csv; charset=utf-8";
        Response.Headers["Content-Disposition"] =
            $"attachment; filename=program-enrollments-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv";

        static string CsvCell(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var needsQuotes =
                value.Contains(',')
                || value.Contains('"')
                || value.Contains('\n')
                || value.Contains('\r');
            return needsQuotes ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
        }

        // BOM UTF-8 (Excel) + encabezado.
        await Response.WriteAsync("\uFEFF", ct);
        await Response.WriteAsync(
            "enrollment_id,patient_id,patient_name,document,status,timezone,"
                + "start_local_date,current_week,total_weeks,xp_balance,"
                + "streak_current,streak_longest,freezes_remaining,created_at\n",
            ct
        );

        var rows = mediator.CreateStream(
            new ExportEnrollmentsQuery(clinicId, from, to, scopedPatientIds),
            ct
        );
        await foreach (var row in rows.WithCancellation(ct))
        {
            var line = string.Create(
                CultureInfo.InvariantCulture,
                $"{row.EnrollmentId},{row.PatientId},{CsvCell(row.PatientName)},{CsvCell(row.DocumentNumber)},{row.Status},{row.Timezone},{row.StartLocalDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)},{row.CurrentWeekNumber},{row.TotalWeeks},{row.XpBalance},{row.StreakCurrent},{row.StreakLongest},{row.FreezesRemaining},{row.CreatedAt:yyyy-MM-dd HH:mm:ss}"
            );
            await Response.WriteAsync(line + "\n", ct);
        }

        return new EmptyResult();
    }

    /// <summary>
    /// Detalle de una inscripción por id (ERP, TASK-10): el shape completo del
    /// listado con <c>currentLevel</c> derivado de la XP. Requiere
    /// <c>Program.View</c>; inscripción inexistente → 404 (anti-IDOR).
    /// </summary>
    [HttpGet("enrollments/{id:guid}")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<ProgramEnrollmentDto>> GetEnrollment(
        Guid id,
        CancellationToken ct
    )
    {
        var result = await mediator.Send(new GetEnrollmentQuery(id), ct);
        return result is null
            ? NotFound(new { message = "Inscripción no encontrada" })
            : Ok(result);
    }

    /// <summary>
    /// Historial de XP (libro mayor) de una inscripción (ERP, TASK-04).
    /// Paginado, orden descendente por fecha de otorgamiento. Requiere
    /// <c>Program.View</c>; inscripción inexistente → 404.
    /// </summary>
    [HttpGet("enrollments/{id:guid}/xp-ledger")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<PaginatedXpLedgerResult>> GetXpLedger(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    )
    {
        var result = await mediator.Send(new GetXpLedgerQuery(id, page, pageSize), ct);
        if (result is null)
        {
            return NotFound(new { message = "Inscripción no encontrada" });
        }

        return Ok(result);
    }

    /// <summary>
    /// Lista las líneas base clínicas de la inscripción (ERP, TASK-05, SPEC
    /// §13.1.2): resuelve el paciente de la inscripción y devuelve sus líneas
    /// base. Requiere <c>Program.View</c>; inscripción inexistente → 404.
    /// </summary>
    [HttpGet("enrollments/{id:guid}/baselines")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<IReadOnlyList<ClinicalBaselineDto>>> GetBaselines(
        Guid id,
        CancellationToken ct
    )
    {
        var result = await mediator.Send(new GetBaselinesQuery(id), ct);
        if (result is null)
        {
            return NotFound(new { message = "Inscripción no encontrada" });
        }

        return Ok(result);
    }

    /// <summary>
    /// Registra una línea base clínica (ERP, TASK-05, SPEC §13.1.2): requiere
    /// <c>Program.Edit</c> + rol clínico del actor (AC-22 — sin rol clínico →
    /// 403; la guardia la enforcea el repositorio vía
    /// <c>UpsertClinicalBaselineAsync</c>). UPSERT por
    /// <c>(patient_id, metric_id)</c>.
    /// </summary>
    [HttpPost("enrollments/{id:guid}/baselines")]
    [RequirePermission("Program.Edit")]
    public async Task<ActionResult<ClinicalBaselineDto>> CreateBaseline(
        Guid id,
        [FromBody] CreateBaselineRequest request,
        CancellationToken ct
    )
    {
        if (actorContext.UserId is not { } userId)
        {
            return Unauthorized(new { message = "Usuario no identificado." });
        }

        var result = await mediator.Send(
            new CreateBaselineCommand(
                id,
                request.MetricId,
                request.Value,
                request.UnitId,
                request.FavorableDirection,
                request.MeasuredAt,
                request.TargetValue,
                userId,
                actorContext.Roles
            ),
            ct
        );

        return Ok(result);
    }

    // ===================== CLÍNICO/ERP: contenido por semana (T-77) =====================

    /// <summary>
    /// Contenido de nutrición y ejercicio configurado por semana para una
    /// inscripción (T-77). Devuelve todas las semanas con su plan/rutina activo
    /// para la ventana de 7 días. Requiere <c>Program.View</c>. Scoping T-81:
    /// 404 si el actor no tiene alcance sobre la inscripción.
    /// </summary>
    [HttpGet("enrollments/{id:guid}/content")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<ProgramContentResponse>> GetProgramContent(
        Guid id,
        CancellationToken ct
    )
    {
        if (!await actorContext.ActorScopedToEnrollmentAsync(id, ct))
        {
            return NotFound(new { message = "Inscripción no encontrada" });
        }

        var result = await mediator.Send(new GetProgramContentQuery(id), ct);
        if (result is null)
        {
            return NotFound(new { message = "Inscripción no encontrada" });
        }

        return Ok(result);
    }

    /// <summary>
    /// Configura el contenido de nutrición y/o ejercicio para una semana
    /// específica de la inscripción (T-77). Body: <c>{ nutritionPlanId?, exerciseRoutineId? }</c>;
    /// null o ausente = desasignar esa dimensión para la semana. Requiere
    /// <c>Program.Edit</c>. Scoping T-81: 404 si el actor no tiene alcance.
    /// </summary>
    [HttpPut("enrollments/{id:guid}/content/week/{weekNumber:int}")]
    [RequirePermission("Program.Edit")]
    public async Task<ActionResult<ProgramContentWeekDto>> SetWeekContent(
        Guid id,
        int weekNumber,
        [FromBody] SetWeekContentRequest request,
        CancellationToken ct
    )
    {
        if (!await actorContext.ActorScopedToEnrollmentAsync(id, ct))
        {
            return NotFound(new { message = "Inscripción no encontrada" });
        }

        return Ok(
            await mediator.Send(
                new SetWeekContentCommand(
                    id,
                    weekNumber,
                    request.NutritionPlanId,
                    request.ExerciseRoutineId,
                    actorContext.UserId
                ),
                ct
            )
        );
    }

    /// <summary>
    /// Asigna el mismo contenido (plan/rutina) a un rango de semanas de la
    /// inscripción (ERP, TASK-13b, UC-C2): el handler reusa
    /// <see cref="SetWeekContentCommand"/> semana a semana en
    /// <c>[fromWeek..toWeek]</c>. Requiere <c>Program.Edit</c>.
    /// </summary>
    [HttpPut("enrollments/{id:guid}/content/range")]
    [RequirePermission("Program.Edit")]
    public async Task<ActionResult<IReadOnlyList<ProgramContentWeekDto>>> SetWeekContentRange(
        Guid id,
        [FromBody] SetWeekContentRangeRequest request,
        CancellationToken ct
    ) =>
        Ok(
            await mediator.Send(
                new SetWeekContentRangeCommand(
                    id,
                    request.FromWeek,
                    request.ToWeek,
                    request.NutritionPlanId,
                    request.ExerciseRoutineId,
                    actorContext.UserId
                ),
                ct
            )
        );

    /// <summary>
    /// Detalle de una semana específica de una inscripción (tareas programadas,
    /// completaciones, puntos y contenido activo). Requiere <c>Program.View</c>
    /// con alcance clínico (T-81): 404 si la inscripción no existe o el actor no
    /// tiene alcance sobre ella.
    /// </summary>
    [HttpGet("enrollments/{id:guid}/week/{weekNumber:int}")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<EnrollmentWeekDetailDto>> GetEnrollmentWeek(
        Guid id,
        int weekNumber,
        CancellationToken ct
    )
    {
        var userId = actorContext.UserId;
        if (userId is null)
        {
            return NotFound(new { message = "Usuario no identificado." });
        }

        if (!await actorContext.ActorScopedToEnrollmentAsync(id, ct))
        {
            return NotFound(new { message = "Inscripción o semana no encontrada" });
        }

        var result = await mediator.Send(
            new GetEnrollmentWeekQuery(id, weekNumber, userId.Value),
            ct
        );

        if (result is null)
        {
            return NotFound(new { message = "Inscripción o semana no encontrada" });
        }

        return Ok(result);
    }

    /// <summary>
    /// Reemplaza las tareas programadas (TasksSnapshot) de una semana específica de una inscripción.
    /// Requiere <c>Program.Edit</c>.
    /// </summary>
    [HttpPut("enrollments/{id:guid}/week/{weekNumber:int}/tasks")]
    [RequirePermission("Program.Edit")]
    public async Task<ActionResult<EnrollmentWeekDetailDto>> ReplaceEnrollmentWeekTasks(
        Guid id,
        int weekNumber,
        [FromBody] IReadOnlyList<WeeklyDayTemplateRequest> tasks,
        CancellationToken ct
    ) =>
        Ok(
            await mediator.Send(
                new ReplaceEnrollmentWeekTasksCommand(id, weekNumber, tasks, actorContext.UserId),
                ct
            )
        );

    // ===================== CLÍNICO/ERP: plantillas =====================

    /// <summary>Listado paginado de plantillas (SPEC §7.6, ERP).</summary>
    [HttpGet("templates")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<PaginatedTemplatesResult>> ListTemplates(
        [FromQuery] string? search,
        [FromQuery] TemplateStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    ) => Ok(await mediator.Send(new ListTemplatesQuery(search, status, page, pageSize), ct));

    /// <summary>Detalle de una plantilla con sus filas por día (SPEC §7.6).</summary>
    [HttpGet("templates/{id:guid}")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<ProgramTemplateDto>> GetTemplate(
        Guid id,
        CancellationToken ct
    ) => Ok(await mediator.Send(new GetTemplateQuery(id), ct));

    /// <summary>Crea una plantilla en estado Draft (SPEC §7.6).</summary>
    [HttpPost("templates")]
    [RequirePermission("Program.Edit")]
    public async Task<ActionResult<ProgramTemplateDto>> CreateTemplate(
        [FromBody] CreateTemplateRequest request,
        CancellationToken ct
    )
    {
        var result = await mediator.Send(
            new CreateTemplateCommand(
                request.Code,
                request.Name,
                request.Description,
                request.TotalWeeks,
                request.Days,
                actorContext.UserId,
                request.TotalDays
            ),
            ct
        );

        return CreatedAtAction(nameof(GetTemplate), new { id = result.Id }, result);
    }

    /// <summary>Actualiza una plantilla existente (SPEC §7.6).</summary>
    [HttpPut("templates/{id:guid}")]
    [RequirePermission("Program.Edit")]
    public async Task<ActionResult<ProgramTemplateDto>> UpdateTemplate(
        Guid id,
        [FromBody] UpdateTemplateRequest request,
        CancellationToken ct
    ) =>
        Ok(
            await mediator.Send(
                new UpdateTemplateCommand(
                    id,
                    request.Code,
                    request.Name,
                    request.Description,
                    request.TotalWeeks,
                    request.Days,
                    actorContext.UserId,
                    request.TotalDays
                ),
                ct
            )
        );

    /// <summary>Publica una plantilla: bump de versión y Draft → Active (SPEC §7.6).</summary>
    [HttpPost("templates/{id:guid}/publish")]
    [RequirePermission("Program.Edit")]
    public async Task<ActionResult<ProgramTemplateDto>> PublishTemplate(
        Guid id,
        CancellationToken ct
    ) => Ok(await mediator.Send(new PublishTemplateCommand(id, actorContext.UserId), ct));

    /// <summary>Archiva una plantilla (idempotente, conserva historia, SPEC §7.6).</summary>
    [HttpPost("templates/{id:guid}/archive")]
    [RequirePermission("Program.Edit")]
    public async Task<ActionResult<ProgramTemplateDto>> ArchiveTemplate(
        Guid id,
        CancellationToken ct
    ) => Ok(await mediator.Send(new ArchiveTemplateCommand(id, actorContext.UserId), ct));

    /// <summary>
    /// Lista las filas por día de una plantilla (SPEC §7.6). Es la misma forma
    /// <c>WeeklyDayTemplateDto</c> del detalle de la plantilla.
    /// </summary>
    [HttpGet("templates/{id:guid}/weekday-tasks")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<IReadOnlyList<WeeklyDayTemplateDto>>> GetWeekdayTasks(
        Guid id,
        CancellationToken ct
    )
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
        CancellationToken ct
    ) =>
        Ok(await mediator.Send(new ReplaceWeekdayTasksCommand(id, tasks, actorContext.UserId), ct));

    // ===================== CLÍNICO/ERP: adaptaciones =====================

    /// <summary>Listado paginado de recomendaciones de adaptación (SPEC §7.7).</summary>
    [HttpGet("adaptations")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<PaginatedAdaptationsResult>> ListAdaptations(
        [FromQuery] Guid? enrollmentId,
        [FromQuery] AdaptationStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    ) =>
        Ok(await mediator.Send(new ListAdaptationsQuery(enrollmentId, status, page, pageSize), ct));

    /// <summary>Detalle de una recomendación de adaptación (SPEC §7.7).</summary>
    [HttpGet("adaptations/{id:guid}")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<AdaptationRecommendationDto>> GetAdaptation(
        Guid id,
        CancellationToken ct
    ) => Ok(await mediator.Send(new GetAdaptationQuery(id), ct));

    /// <summary>Aprueba o rechaza una recomendación de adaptación (SPEC §7.7).</summary>
    [HttpPost("adaptations/{id:guid}/decide")]
    [RequirePermission("Program.Adapt")]
    public async Task<ActionResult<AdaptationRecommendationDto>> DecideAdaptation(
        Guid id,
        [FromBody] DecideAdaptationRequest request,
        CancellationToken ct
    ) =>
        Ok(
            await mediator.Send(
                new DecideAdaptationCommand(
                    id,
                    request.Decision,
                    request.Note,
                    actorContext.UserId
                ),
                ct
            )
        );

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
            return NotFound(
                new { message = "No existe un perfil de paciente para el usuario autenticado." }
            );
        }

        return Ok(await mediator.Send(new GetScoresQuery(patientId.Value), ct));
    }

    /// <summary>
    /// Historial de puntajes del paciente autenticado (pestaña Evolución del
    /// móvil): serie semanal ASCENDENTE con los Índices de Salud y de
    /// Transformación PERSISTIDOS (nunca dispara recálculo ni al motor de
    /// puntajes — la frescura de la semana actual es trabajo de
    /// <c>GET /scores</c>). Self-service del paciente: SOLO <c>[Authorize]</c>
    /// (convención <c>me/*</c> — los JWT de paciente no llevan claims de
    /// permiso, igual que la liga). El <c>patientId</c> se resuelve del JWT
    /// vía <see cref="IProgramActorContext"/> (nunca del body, anti-IDOR
    /// AC-11): sin perfil de paciente → 404; sin inscripción activa → 404
    /// <c>NO_ACTIVE_ENROLLMENT</c>. <c>weeks</c> opcional (default 12, clamp
    /// 1..83). Serie cacheada 5 min por paciente (fail-open); el recorte a las
    /// últimas N semanas se aplica por request.
    /// </summary>
    [HttpGet("me/scores-history")]
    public async Task<ActionResult<ScoresHistoryResponseDto>> GetScoresHistory(
        int? weeks,
        CancellationToken ct
    )
    {
        var patientId = await actorContext.ResolvePatientProfileIdAsync(ct);
        if (patientId is null)
        {
            return NotFound(
                new { message = "No existe un perfil de paciente para el usuario autenticado." }
            );
        }

        return Ok(
            await mediator.Send(
                new GetScoresHistoryQuery(
                    patientId.Value,
                    weeks ?? GetScoresHistoryQuery.DefaultWeeks
                ),
                ct
            )
        );
    }

    // ===================== PACIENTE: liga (LEAGUE v1) =====================

    /// <summary>
    /// Preferencias de la liga del paciente autenticado (LEAGUE v1):
    /// <c>{ nickname: string|null, optIn: boolean }</c>. El opt-in es default
    /// OFF (privacidad por diseño) y el nickname es el seudónimo público que
    /// se muestra en el ranking (obligatorio para optar). Self-service del
    /// paciente: SOLO <c>[Authorize]</c> (convención <c>me/*</c>, sin permisos
    /// granulares en v1 — los JWT de paciente no llevan claims de permiso).
    /// El <c>patientId</c> SIEMPRE viene del JWT vía
    /// <see cref="IProgramActorContext"/> (nunca del body, anti-IDOR AC-11).
    /// Validación FluentValidation (formato/longitud del nickname, opt-in sin
    /// nickname, tokens reservados) → 400; sin perfil de paciente → 404.
    /// Persiste SOLO las dos columnas de la liga; el resto del perfil no se
    /// toca. Al guardar invalida el cohorte cacheado del paciente (revocación
    /// del consentimiento inmediata).
    /// </summary>
    [HttpPut("me/league-preferences")]
    public async Task<ActionResult<LeaguePreferencesDto>> UpdateLeaguePreferences(
        [FromBody] UpdateLeaguePreferencesRequest request,
        CancellationToken ct
    )
    {
        var patientId = await actorContext.ResolvePatientProfileIdAsync(ct);
        if (patientId is null)
        {
            return NotFound(
                new { message = "No existe un perfil de paciente para el usuario autenticado." }
            );
        }

        return Ok(
            await mediator.Send(
                new UpdateLeaguePreferencesCommand(
                    patientId.Value,
                    request.Nickname,
                    request.OptIn
                ),
                ct
            )
        );
    }

    /// <summary>
    /// Liga del paciente autenticado (LEAGUE v1): cohorte (estado o nacional
    /// por la k-rule) con las 4 categorías (racha/evo/adh/clin) y el ranking
    /// propio. Privacidad por diseño: opt-in default OFF, seudonimización
    /// server-side (la respuesta nunca contiene id, nombre real o ciudad de
    /// otro participante) y k-rule (estado con &lt;10 opt-in → cohorte
    /// nacional). Self-service del paciente: SOLO <c>[Authorize]</c>
    /// (convención <c>me/*</c> — los JWT de paciente no llevan claims de
    /// permiso; los permisos granulares del módulo son batch B7, pendiente).
    /// El <c>patientId</c> se resuelve del JWT (nunca del body); sin perfil o
    /// sin inscripción activa → 404. Cohortes cacheados 5 min (Valkey,
    /// fail-open); el bloque <c>me</c> y los flags <c>isMe</c> se resuelven
    /// por request contra el JWT.
    /// </summary>
    [HttpGet("me/league")]
    public async Task<ActionResult<LeagueResponseDto>> GetLeague(CancellationToken ct)
    {
        var patientId = await actorContext.ResolvePatientProfileIdAsync(ct);
        if (patientId is null)
        {
            return NotFound(
                new { message = "No existe un perfil de paciente para el usuario autenticado." }
            );
        }

        return Ok(await mediator.Send(new GetLeagueQuery(patientId.Value), ct));
    }

    /// <summary>
    /// Historial de métricas clínicas del paciente autenticado (metrics-history,
    /// Home del móvil): series REALES por fecha local de
    /// <c>app.clinical_measurements</c> para los códigos solicitados
    /// (<c>codes</c> CSV, default <c>bmi,hba1c,body_fat</c>; códigos
    /// inexistentes → 400 con la lista de válidos) en la ventana de
    /// <c>days</c> (default 180, clamp 7..365). Incluye la talla del perfil,
    /// el rango de referencia (target) y la dirección favorable. Self-service
    /// del paciente: SOLO <c>[Authorize]</c> (convención <c>me/*</c>); el
    /// <c>patientId</c> SIEMPRE del JWT vía
    /// <see cref="IProgramActorContext"/> (anti-IDOR AC-11); sin inscripción
    /// activa → 404. Cache 5 min por paciente (fail-open).
    /// </summary>
    [HttpPost("me/weight")]
    public async Task<ActionResult<RecordedWeightDto>> RecordWeight(
        [FromBody] RecordWeightRequest request, CancellationToken ct)
    {
        var patientId = await actorContext.ResolvePatientProfileIdAsync(ct);
        var enrollmentId = await actorContext.ResolveActiveEnrollmentIdAsync(ct);
        if (patientId is null || enrollmentId is null)
            return NotFound(new { code = "NO_ACTIVE_ENROLLMENT", message = "Se requiere perfil de paciente e inscripción activa." });
        if (actorContext.UserId is not { } actorId) return Unauthorized();
        var result = await mediator.Send(new RecordWeightCommand(patientId.Value, enrollmentId.Value,
            actorId, request.WeightKg, request.Date), ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("me/metrics-history")]
    public async Task<ActionResult<MetricsHistoryResponseDto>> GetMetricsHistory(
        [FromQuery] string? codes,
        [FromQuery] int? days,
        CancellationToken ct
    )
    {
        var patientId = await actorContext.ResolvePatientProfileIdAsync(ct);
        if (patientId is null)
        {
            return NotFound(
                new { message = "No existe un perfil de paciente para el usuario autenticado." }
            );
        }

        var parsedCodes = string.IsNullOrWhiteSpace(codes)
            ? []
            : codes.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            );

        return Ok(
            await mediator.Send(
                new GetMetricsHistoryQuery(patientId.Value, parsedCodes, days ?? 0),
                ct
            )
        );
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
        CancellationToken ct
    )
    {
        if (request.PatientId == Guid.Empty)
        {
            return BadRequest(new { message = "El patientId es requerido." });
        }

        Response.Headers["X-Score-Recalculated"] = "true";

        return Ok(
            await mediator.Send(
                new CalculateScoresCommand(request.PatientId, request.PeriodEndLocalDate),
                ct
            )
        );
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
    public async Task<ActionResult<IReadOnlyList<XpRuleDto>>> ListXpRules(CancellationToken ct) =>
        Ok(await mediator.Send(new ListXpRulesQuery(), ct));

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
        CancellationToken ct
    ) =>
        Ok(
            await mediator.Send(
                new UpdateXpRuleCommand(
                    code,
                    request.BaseXp,
                    request.Multiplier,
                    request.MaxPerDay,
                    request.MaxPerWeek,
                    request.RequiresValidation,
                    request.Active,
                    request.ValidUntil,
                    actorContext.UserId
                ),
                ct
            )
        );

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
        CancellationToken ct = default
    ) => Ok(await mediator.Send(new ListClinicalReviewsQuery(page, pageSize), ct));

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
        CancellationToken ct
    ) =>
        Ok(
            await mediator.Send(
                new DecideClinicalReviewCommand(
                    id,
                    request.Approve,
                    actorContext.UserId,
                    actorContext.Roles
                ),
                ct
            )
        );

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
    /// programa no se auto-completa aquí). Un log duplicado de una comida
    /// (des/alm/mer/cen) → 409 <c>HABIT_ALREADY_LOGGED</c>; la XP nunca se
    /// duplica. El <c>agua</c> es acumulable: repetirla el mismo día responde
    /// 200 con XP 0 y actualiza el total acumulado <c>waterMl</c> (la XP de
    /// hidratación es 1/día, primer log).
    /// El <c>patientId</c> se resuelve del JWT (nunca del body): sin perfil de
    /// paciente → 404 (anti-IDOR AC-11).
    /// </summary>
    [HttpPost("nutrition/log")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<NutritionLogResultDto>> LogNutrition(
        [FromBody] LogNutritionRequest request,
        CancellationToken ct
    )
    {
        var patientId = await actorContext.ResolvePatientProfileIdAsync(ct);
        if (patientId is null)
        {
            return NotFound(
                new { message = "No existe un perfil de paciente para el usuario autenticado." }
            );
        }

        var result = await mediator.Send(
            new LogNutritionCommand(
                patientId.Value,
                request.MealCode,
                request.LocalDate,
                actorContext.UserId,
                request.Intake
            ),
            ct
        );

        logger.LogInformation(
            "Program.NutritionLog: meal={MealCode} fecha={LocalDate} xpAwarded={XpAwarded}",
            result.MealCode,
            result.LocalDate,
            result.XpAwarded
        );

        return Ok(result);
    }

    /// <summary>
    /// Edita el intake de una comida/hidratación ya registrada (móvil):
    /// actualiza la fila anclada al MISMO <c>habit_check</c> (sin duplicados,
    /// sin XP adicional). Sin log para esa comida/fecha → 404
    /// <c>NUTRITION_LOG_NOT_FOUND</c>. Mismo permiso y resolución de paciente
    /// que el POST.
    /// </summary>
    [HttpPut("nutrition/log/{mealCode}")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<NutritionLogResultDto>> UpdateNutritionLog(
        string mealCode,
        [FromBody] UpdateNutritionIntakeRequest? request,
        CancellationToken ct
    )
    {
        if (
            !Enum.TryParse<MealCode>(mealCode, ignoreCase: true, out var code)
            || !Enum.IsDefined(code)
        )
        {
            return BadRequest(
                new { message = "Código de comida/hidratación inválido (des/alm/mer/cen/agua)." }
            );
        }

        var patientId = await actorContext.ResolvePatientProfileIdAsync(ct);
        if (patientId is null)
        {
            return NotFound(
                new { message = "No existe un perfil de paciente para el usuario autenticado." }
            );
        }

        var result = await mediator.Send(
            new UpdateNutritionIntakeCommand(
                patientId.Value,
                code,
                request?.LocalDate,
                actorContext.UserId,
                request?.Intake
            ),
            ct
        );

        return Ok(result);
    }

    // ============ PACIENTE: notificaciones gamificadas (SPEC §20, D) ============

    /// <summary>
    /// Centro de notificaciones gamificadas del paciente autenticado (SPEC §20,
    /// D): log de hitos de racha / racha del nutracéutico / subida de nivel /
    /// día perfecto, paginado (default 20, orden descendente por fecha), con
    /// <c>readAt</c> por fila y el <c>unreadCount</c> total para el badge del
    /// móvil. El <c>patientId</c> se resuelve del JWT (nunca del body): sin
    /// perfil de paciente → 404 (anti-IDOR AC-11).
    /// </summary>
    [HttpGet("notifications")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<PaginatedNotificationsResult>> ListNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    )
    {
        var patientId = await actorContext.ResolvePatientProfileIdAsync(ct);
        if (patientId is null)
        {
            return NotFound(
                new { message = "No existe un perfil de paciente para el usuario autenticado." }
            );
        }

        return Ok(
            await mediator.Send(new ListNotificationsQuery(patientId.Value, page, pageSize), ct)
        );
    }

    /// <summary>
    /// Marca una notificación del paciente autenticado como leída (SPEC §20, D):
    /// <c>read_at = now</c>. La notificación debe pertenecer al paciente
    /// (anti-IDOR AC-11): si no, 404 sin distinguir si existe.
    /// </summary>
    [HttpPost("notifications/{id:guid}/read")]
    [RequirePermission("Program.View")]
    public async Task<IActionResult> MarkNotificationRead(Guid id, CancellationToken ct)
    {
        var patientId = await actorContext.ResolvePatientProfileIdAsync(ct);
        if (patientId is null)
        {
            return NotFound(
                new { message = "No existe un perfil de paciente para el usuario autenticado." }
            );
        }

        var marked = await mediator.Send(new MarkNotificationReadCommand(id, patientId.Value), ct);
        if (!marked)
        {
            return NotFound(new { message = "Notificación no encontrada" });
        }

        return NoContent();
    }

    // ============ DEBILIDADES DEL PACIENTE (SPEC §21, D — "Paso 7c") ============

    /// <summary>
    /// Debilidades del paciente autenticado (SPEC §21, D): hallazgos del motor
    /// de detección (y de los profesionales) paginados (default 20, orden
    /// descendente por <c>detectedAt</c>), con categoría, severidad, indicador
    /// y ciclo de vida. El <c>patientId</c> se resuelve del JWT (nunca del
    /// body): sin perfil de paciente → 404 (anti-IDOR AC-11).
    /// </summary>
    [HttpGet("weaknesses")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<PaginatedWeaknessesResult>> ListWeaknesses(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    )
    {
        var patientId = await actorContext.ResolvePatientProfileIdAsync(ct);
        if (patientId is null)
        {
            return NotFound(
                new { message = "No existe un perfil de paciente para el usuario autenticado." }
            );
        }

        return Ok(
            await mediator.Send(new ListWeaknessesQuery(patientId.Value, page, pageSize), ct)
        );
    }

    /// <summary>
    /// Cola clínica de debilidades abiertas (SPEC §21, D): los hallazgos
    /// <c>status = 'open'</c> que esperan decisión de un clínico, paginados
    /// (default 20, orden ascendente FIFO por <c>detectedAt</c> — la más
    /// antigua primero). Requiere <c>Program.Adapt</c> (decisión clínica del
    /// módulo). Es la vista previa de
    /// <c>POST /api/v1/program/weaknesses/{{id}}/status</c>.
    /// </summary>
    [HttpGet("weaknesses/open")]
    [RequirePermission("Program.Adapt")]
    public async Task<ActionResult<PaginatedWeaknessesResult>> ListOpenWeaknesses(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    ) => Ok(await mediator.Send(new ListOpenWeaknessesQuery(page, pageSize), ct));

    /// <summary>
    /// Transición de estado de una debilidad (SPEC §21, D — AC-44):
    /// <c>{ status: 'acknowledged'|'in_intervention'|'resolved'|'dismissed' }</c>.
    /// Requiere <c>Program.Adapt</c> + rol clínico del actor (AC-22: un paciente
    /// cambiando el estado de su propia debilidad → 403). Transición
    /// idempotente (aplicar el mismo estado no es error); <c>resolved</c> fija
    /// <c>resolvedAt</c>.
    /// </summary>
    [HttpPost("weaknesses/{id:guid}/status")]
    [RequirePermission("Program.Adapt")]
    public async Task<ActionResult<WeaknessDto>> UpdateWeaknessStatus(
        Guid id,
        [FromBody] UpdateWeaknessStatusRequest request,
        CancellationToken ct
    ) =>
        Ok(
            await mediator.Send(
                new UpdateWeaknessStatusCommand(
                    id,
                    request.Status,
                    actorContext.UserId,
                    actorContext.Roles
                ),
                ct
            )
        );

    // ============ INTERVENCIONES (SPEC §22, "Paso 7d") ============

    /// <summary>
    /// Intervenciones del paciente autenticado (SPEC §22, D): listado paginado
    /// (default 20, orden descendente por <c>createdAt</c>) de las intervenciones
    /// derivadas de debilidades y registradas por profesionales. El
    /// <c>patientId</c> se resuelve del JWT (nunca del body): sin perfil de
    /// paciente → 404 (anti-IDOR AC-11).
    /// </summary>
    [HttpGet("interventions")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<PaginatedInterventionsResult>> ListInterventions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    )
    {
        var patientId = await actorContext.ResolvePatientProfileIdAsync(ct);
        if (patientId is null)
        {
            return NotFound(
                new { message = "No existe un perfil de paciente para el usuario autenticado." }
            );
        }

        return Ok(
            await mediator.Send(new ListInterventionsQuery(patientId.Value, page, pageSize), ct)
        );
    }

    /// <summary>
    /// Paciente acepta una intervención (SPEC §22, D — AC-47):
    /// <c>detected→accepted</c>, fija <c>accepted_at</c>, otorga
    /// <c>INTERV_ACCEPT</c> (+15). Si es <c>recovery_mission</c> también
    /// <c>RECOVERY_MISSION</c> (+50). La intervención debe pertenecer al
    /// paciente autenticado (anti-IDOR AC-11). Estado inválido → 409.
    /// </summary>
    [HttpPost("interventions/{id:guid}/accept")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<InterventionDto>> AcceptIntervention(
        Guid id,
        CancellationToken ct
    )
    {
        var patientId = await actorContext.ResolvePatientProfileIdAsync(ct);
        if (patientId is null)
        {
            return NotFound(
                new { message = "No existe un perfil de paciente para el usuario autenticado." }
            );
        }

        return Ok(await mediator.Send(new AcceptInterventionCommand(id, patientId.Value), ct));
    }

    /// <summary>
    /// Cola clínica de intervenciones abiertas (SPEC §22, D): las
    /// intervenciones con <c>status != 'completed'</c> que esperan decisión,
    /// paginadas (default 20, orden ascendente FIFO por <c>createdAt</c>).
    /// Requiere <c>Program.Adapt</c> (decisión clínica del módulo).
    /// </summary>
    [HttpGet("interventions/open")]
    [RequirePermission("Program.Adapt")]
    public async Task<ActionResult<PaginatedInterventionsResult>> ListOpenInterventions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    ) => Ok(await mediator.Send(new ListOpenInterventionsQuery(page, pageSize), ct));

    /// <summary>
    /// Clínico actualiza el estado de una intervención (SPEC §22, D — AC-48):
    /// <c>{ status, result?, assignedTo? }</c>. Requiere <c>Program.Adapt</c> +
    /// rol clínico del actor (AC-22). <c>completed</c> requiere resultado y
    /// otorga <c>INTERV_COMPLETE</c> (+200, validated_by). Estado inválido → 409.
    /// </summary>
    [HttpPost("interventions/{id:guid}/status")]
    [RequirePermission("Program.Adapt")]
    public async Task<ActionResult<InterventionDto>> UpdateInterventionStatus(
        Guid id,
        [FromBody] UpdateInterventionStatusRequest request,
        CancellationToken ct
    ) =>
        Ok(
            await mediator.Send(
                new UpdateInterventionStatusCommand(
                    id,
                    request.Status,
                    actorContext.UserId,
                    actorContext.Roles,
                    request.Result,
                    request.AssignedTo
                ),
                ct
            )
        );

    /// <summary>
    /// Hook de telemedicina: teleconsulta agendada (SPEC §22, D — AC-49):
    /// otorga <c>TELE_SCHEDULE</c> (+50). Callable por el servicio de
    /// telemedicina o por un clínico con <c>Program.Adapt</c>.
    /// </summary>
    [HttpPost("interventions/{id:guid}/tele-scheduled")]
    [RequirePermission("Program.Adapt")]
    public async Task<ActionResult<InterventionDto>> MarkTeleScheduled(
        Guid id,
        CancellationToken ct
    ) => Ok(await mediator.Send(new MarkTeleScheduledCommand(id), ct));

    /// <summary>
    /// Hook de telemedicina: asistencia confirmada por el clínico
    /// (SPEC §22, D — AC-49): otorga <c>TELE_ATTEND</c> (+100, validated_by)
    /// y mueve la intervención a <c>in_progress</c>.
    /// </summary>
    [HttpPost("interventions/{id:guid}/tele-attended")]
    [RequirePermission("Program.Adapt")]
    public async Task<ActionResult<InterventionDto>> MarkTeleAttended(Guid id, CancellationToken ct)
    {
        if (actorContext.UserId is not { } userId)
        {
            return Unauthorized(new { message = "Usuario no identificado." });
        }

        return Ok(await mediator.Send(new MarkTeleAttendedCommand(id, userId), ct));
    }

    /// <summary>
    /// Hook de telemedicina: cumplimiento evaluado (SPEC §22, D — AC-49):
    /// otorga <c>TELE_COMPLY</c> (+50) y puede avanzar hacia
    /// <c>completed</c>.
    /// </summary>
    [HttpPost("interventions/{id:guid}/tele-comply")]
    [RequirePermission("Program.Adapt")]
    public async Task<ActionResult<InterventionDto>> MarkTeleComply(
        Guid id,
        CancellationToken ct
    ) => Ok(await mediator.Send(new MarkTeleComplyCommand(id), ct));

    // ===================== ERP: gamificación (SPEC §23) =====================

    /// <summary>
    /// Dashboard ERP del monitoreo comunitario (SPEC §23, AC-50): KPIs de adherencia,
    /// XP semanal, rachas, tendencias y leaderboard.
    /// </summary>
    [HttpGet("erp/dashboard")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<ProgramErpDashboardDto>> GetErpDashboard(CancellationToken ct) =>
        Ok(await mediator.Send(new GetErpDashboardQuery(), ct));

    // ===================== ERP: Biometría (SPEC §06) =====================

    /// <summary>
    /// Resumen comunitario de Biometría: promedios de IMC/grasa/glucosa, distribuciones,
    /// evolución semanal, ciudades y alertas. (SPEC §06, dashboard comunitario).
    /// </summary>
    [HttpGet("erp/biometria/community")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<BiometriaCommunityDto>> GetBiometriaCommunity(
        CancellationToken ct
    ) => Ok(await mediator.Send(new GetBiometriaCommunityQuery(), ct));

    /// <summary>
    /// Listado paginado de pacientes con indicadores de biometría (última medición por paciente).
    /// Filtros: search, gender, imcCategory, glucosaCategory.
    /// </summary>
    [HttpGet("erp/biometria/patients")]
    [RequirePermission("Program.View")]
    public async Task<
        ActionResult<PaginatedResult<BiometriaPatientListItemDto>>
    > ListBiometriaPatients(
        [FromQuery] string? search = null,
        [FromQuery] string? gender = null,
        [FromQuery] string? imcCategory = null,
        [FromQuery] string? glucosaCategory = null,
        [FromQuery] string? grasaCategory = null,
        [FromQuery] string? trend = null,
        [FromQuery] Guid? cityId = null,
        [FromQuery] string? stateAbbr = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    )
    {
        var (items, total) = await mediator.Send(
            new ListBiometriaPatientsQuery(
                search,
                gender,
                imcCategory,
                glucosaCategory,
                grasaCategory,
                trend,
                cityId,
                stateAbbr,
                page,
                pageSize
            ),
            ct
        );

        var totalPages = (int)Math.Ceiling((double)total / Math.Clamp(pageSize, 1, 100));
        return Ok(
            new PaginatedResult<BiometriaPatientListItemDto>(
                items,
                total,
                page,
                pageSize,
                totalPages
            )
        );
    }

    /// <summary>
    /// Detalle de biometría de un paciente: historial semanal, heatmap y datos exactos.
    /// </summary>
    [HttpGet("erp/biometria/patients/{id:guid}")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<BiometriaPatientDetailDto>> GetBiometriaPatient(
        Guid id,
        CancellationToken ct
    )
    {
        var result = await mediator.Send(new GetBiometriaPatientQuery(id), ct);
        if (result is null)
        {
            return NotFound(new { message = "Paciente sin inscripción activa" });
        }
        return Ok(result);
    }

    /// <summary>
    /// Exporte CSV de biometría de pacientes (text/csv con headers).
    /// </summary>
    [HttpGet("erp/biometria/export/csv")]
    [RequirePermission("Program.View")]
    public async Task<IActionResult> ExportBiometriaCsv(CancellationToken ct)
    {
        Response.ContentType = "text/csv; charset=utf-8";
        Response.Headers["Content-Disposition"] =
            $"attachment; filename=biometria-pacientes-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv";

        await Response.WriteAsync("\uFEFF", ct);
        await Response.WriteAsync(
            "patient_id,name,gender,age,city,weight,height,imc,imc_category,"
                + "waist,hip,icc,pct_grasa,pct_grasa_category,glucosa,glucosa_category,"
                + "week_number,streak,trend\n",
            ct
        );

        var rows = mediator.CreateStream(new StreamBiometriaPatientsExportQuery(), ct);
        await foreach (var row in rows.WithCancellation(ct))
        {
            await Response.WriteAsync(
                $"{row.PatientId},{CsvCell(row.Name)},{row.Gender ?? ""},{row.Age?.ToString() ?? ""},"
                    + $"{CsvCell(row.City)},{row.Weight?.ToString() ?? ""},{row.Height?.ToString() ?? ""},"
                    + $"{row.Imc?.ToString() ?? ""},{CsvCell(row.ImcCategory)},"
                    + $"{row.Waist?.ToString() ?? ""},{row.Hip?.ToString() ?? ""},{row.Icc?.ToString() ?? ""},"
                    + $"{row.PctGrasa?.ToString() ?? ""},{CsvCell(row.PctGrasaCategory)},"
                    + $"{row.Glucosa?.ToString() ?? ""},{CsvCell(row.GlucosaCategory)},"
                    + $"{row.WeekNumber?.ToString() ?? ""},{row.Streak},{CsvCell(row.Trend)}\n",
                ct
            );
        }

        return new EmptyResult();
    }

    /// <summary>
    /// Vista de hoy para el ERP (SPEC §23, AC-51): KPIs por misión, feed en vivo,
    /// pendientes críticos y heatmap semanal.
    /// </summary>
    [HttpGet("erp/today")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<ProgramErpTodayDto>> GetErpToday(CancellationToken ct) =>
        Ok(await mediator.Send(new GetErpTodayQuery(), ct));

    /// <summary>
    /// Vista de adherencia ERP (SPEC §23, AC-52): tendencia 8 semanas, ranking de
    /// rachas y tabla paginada con adherencia por misión.
    /// </summary>
    [HttpGet("erp/adherencia")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<ProgramErpAdherenciaDto>> GetErpAdherencia(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null,
        CancellationToken ct = default
    ) =>
        Ok(
            await mediator.Send(
                new GetErpAdherenciaQuery(page, pageSize, search, sortBy, sortDir),
                ct
            )
        );

    /// <summary>
    /// Vista de cofres/rachas ERP (SPEC §23, AC-53): XP por categoría, milestones
    /// de racha y tabla paginada de pacientes con nivel/nb_streak.
    /// </summary>
    [HttpGet("erp/cofres")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<ProgramErpCofresDto>> GetErpCofres(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null,
        CancellationToken ct = default
    ) =>
        Ok(await mediator.Send(new GetErpCofresQuery(page, pageSize, search, sortBy, sortDir), ct));

    /// <summary>
    /// Perfil 360 de un paciente (SPEC §23, AC-54): inscripción, racha, XP,
    /// scores, debilidades, intervenciones y checkins.
    /// </summary>
    [HttpGet("erp/patients/{patientId:guid}/overview")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<PatientOverviewDto>> GetPatientOverview(
        Guid patientId,
        CancellationToken ct
    )
    {
        var result = await mediator.Send(new GetPatientOverviewQuery(patientId), ct);
        if (result is null)
        {
            return NotFound(new { message = "Paciente sin inscripción activa" });
        }

        return Ok(result);
    }

    /// <summary>
    /// Controles del programa de un paciente (UC-004): línea de tiempo de los
    /// hitos configurados (7/14/21/45/60/90) con su ciclo de vida
    /// (envío/respuesta/follow-up/cierre), control abierto vigente, próximo
    /// vencimiento, adherencia agregada y el documento del lote de examen
    /// asociado a cada control. 404 si el paciente no tiene inscripción activa
    /// (mismo criterio que el overview 360). Scoping T-81: 404 si el actor
    /// (paciente o clínico) no tiene alcance sobre el paciente; los roles de
    /// administración (Admin/OrganizationAdmin/ClinicAdmin) no se filtran.
    /// </summary>
    [HttpGet("erp/patients/{patientId:guid}/controls")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<PatientControlsDto>> GetPatientControls(
        Guid patientId,
        CancellationToken ct
    )
    {
        var scopedPatientIds = await actorContext.ResolveScopedPatientIdsAsync(ct);
        if (scopedPatientIds is not null && !scopedPatientIds.Contains(patientId))
        {
            return NotFound(new { message = "Paciente sin inscripción activa" });
        }

        var result = await mediator.Send(new GetPatientControlsQuery(patientId), ct);
        if (result is null)
        {
            return NotFound(new { message = "Paciente sin inscripción activa" });
        }

        return Ok(result);
    }

    // ===================== ERP: mantenimiento (B12) =====================

    /// <summary>
    /// Disparo manual de la reconciliación de rachas (B12, T-28): recalcula
    /// <c>streak_states</c> de las inscripciones activas desde
    /// <c>task_completions</c> + congelamientos consumidos y corrige desviaciones
    /// (idempotente). El mismo camino corre nocturno vía hosted service
    /// (<c>Program:Reconciliation</c>). Requiere <c>Program.Edit</c>.
    /// </summary>
    [HttpPost("maintenance/reconcile-streaks")]
    [RequirePermission(PermissionCodes.ProgramEdit)]
    public async Task<ActionResult<StreakReconciliationSummary>> ReconcileStreaks(
        CancellationToken ct
    ) => Ok(await mediator.Send(new ReconcileStreaksCommand(), ct));

    // ===================== ERP: bitácora de actividad =====================

    /// <summary>
    /// Bitácora de actividad del módulo (ERP): entradas del log de auditoría
    /// trigger-based (<c>audit.activity_logs</c>) filtradas a las tablas
    /// <c>app.*</c> del módulo, paginadas, con filtros opcionales por tabla,
    /// acción (INSERT/UPDATE/DELETE), ventana de fechas y actor (email
    /// contiene). Requiere <c>Program.View</c>. Solo lectura: nunca expone el
    /// detalle crudo de filas (<c>old_data</c>/<c>new_data</c>, posible PHI).
    /// </summary>
    [HttpGet("activity-log")]
    [RequirePermission("Program.View")]
    public async Task<ActionResult<PaginatedActivityLogResult>> GetActivityLog(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? table = null,
        [FromQuery] string? action = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] string? actor = null,
        CancellationToken ct = default
    ) =>
        Ok(
            await mediator.Send(
                new ListProgramActivityLogQuery(page, pageSize, table, action, from, to, actor),
                ct
            )
        );

    // ===================== helpers =====================

    /// <summary>
    /// <summary>
    /// Resuelve las URLs de storage (<c>thumbnailUrl</c> y <c>mediaUrl</c>) de cada tarea del snapshot a su forma
    /// final de URL (SPEC §7.1 + T-13): con el proveedor S3 un presign real con 1h de validez; con
    /// el proveedor Local el proxy firmado del backend <c>{host}/api/v1/storage/{key}?exp&amp;sig</c>
    /// preservando la totalidad de propiedades del DTO (audio, autor, capítulos, etc.).
    /// </summary>
    private async Task<ProgramSnapshotDto> ResolveMediaUrlsAsync(
        ProgramSnapshotDto snapshot,
        CancellationToken ct
    )
    {
        if (snapshot.TodayTasks.Count == 0)
        {
            return snapshot;
        }

        var tasks = new List<TodayTaskDto>(snapshot.TodayTasks.Count);
        foreach (var task in snapshot.TodayTasks)
        {
            if (task.Content is null)
            {
                tasks.Add(task);
                continue;
            }

            var resolvedThumbnail = await ResolveMediaUrlAsync(task.Content.ThumbnailUrl, ct);
            var resolvedMedia = await ResolveMediaUrlAsync(task.Content.MediaUrl, ct);

            tasks.Add(
                task with
                {
                    Content = task.Content with
                    {
                        ThumbnailUrl = resolvedThumbnail,
                        MediaUrl = resolvedMedia,
                    },
                }
            );
        }

        return snapshot with
        {
            TodayTasks = tasks,
        };
    }

    /// <summary>Forma final de URL de un objeto de storage (key → URL).</summary>
    private async Task<string?> ResolveMediaUrlAsync(string? key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        // Ya es una URL absoluta (precedente: media ya resueltos por el repo).
        if (
            key.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        )
        {
            return key;
        }

        if (objectStorage.IsCloudStorage)
        {
            return await objectStorage.GetPreSignedUrlAsync(key, TimeSpan.FromHours(1), ct);
        }

        if (signatureService is not null)
        {
            var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
            var signature = signatureService.Sign(key, expiresAt);
            return $"{Request.Scheme}://{Request.Host}/api/v1/storage/{key}?exp={expiresAt.ToUnixTimeSeconds()}&sig={signature}";
        }

        return $"{Request.Scheme}://{Request.Host}/api/v1/storage/{key}";
    }

    /// <summary>Código de la plantilla por defecto (config, fallback <c>default-83w</c>).</summary>
    private string DefaultTemplateCode() =>
        configuration[DefaultTemplateCodeKey] ?? DefaultTemplateCodeFallback;

    /// <summary>Escapa un valor para celda CSV.</summary>
    private static string CsvCell(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var needsQuotes =
            value.Contains(',')
            || value.Contains('"')
            || value.Contains('\n')
            || value.Contains('\r');
        return needsQuotes ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
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
    string? ContentFingerprint,
    VitalsPayload? Vitals = null
);

/// <summary>
/// Payload de inscripción (<c>POST /enrollments</c> clínico y
/// <c>POST /enrollments/me</c> paciente, SPEC §7.5). En el flujo del paciente el
/// <c>patientId</c> se ignora: la identidad sale del JWT (SPEC §6.14).
/// </summary>
public sealed record EnrollRequest(
    Guid? PatientId,
    Guid? TemplateId,
    string Timezone,
    DateOnly? StartLocalDate
);

/// <summary>Body de pause/withdraw: motivo informativo (no persiste en MVP, se loguea).</summary>
public sealed record EnrollmentActionRequest(string? Reason);

/// <summary>Payload de la inscripción masiva (<c>POST /enrollments/bulk</c>, B13).</summary>
public sealed record BulkEnrollRequest(
    IReadOnlyList<Guid> PatientIds,
    Guid? TemplateId,
    string Timezone,
    DateOnly? StartLocalDate
);

/// <summary>
/// Payload de <c>POST /api/v1/program/enrollments/{id}/baselines</c>
/// (ERP, TASK-05): métrica del catálogo, valor con su unidad, dirección
/// favorable, fecha local de medición y target clínico opcional.
/// </summary>
public sealed record CreateBaselineRequest(
    Guid MetricId,
    decimal Value,
    Guid UnitId,
    FavorableDirection FavorableDirection,
    DateOnly MeasuredAt,
    decimal? TargetValue
);

/// <summary>
/// Payload de <c>PUT /api/v1/program/enrollments/{id}/content/range</c>
/// (ERP, TASK-13b): aplica el mismo plan/rutina (o desasigna, si ambos son
/// null) a todas las semanas de <c>[fromWeek..toWeek]</c>.
/// </summary>
public sealed record SetWeekContentRangeRequest(
    int FromWeek,
    int ToWeek,
    Guid? NutritionPlanId,
    Guid? ExerciseRoutineId
);

/// <summary>Payload de creación de plantilla (SPEC §7.6).</summary>
public sealed record CreateTemplateRequest(
    string Code,
    string Name,
    string? Description,
    int? TotalWeeks = null,
    IReadOnlyList<WeeklyDayTemplateRequest>? Days = null,
    int? TotalDays = null
);

/// <summary>Payload de actualización de plantilla (SPEC §7.6).</summary>
public sealed record UpdateTemplateRequest(
    string Code,
    string Name,
    string? Description,
    int? TotalWeeks = null,
    IReadOnlyList<WeeklyDayTemplateRequest>? Days = null,
    int? TotalDays = null
);

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
    DateOnly? ValidUntil
);

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
/// hoy local; una fecha futura → 422 <c>INVALID_DATE</c>). <c>Intake</c> es el
/// bloque opcional de macros/hidratación (SPEC nutrition-intake-adherence):
/// clientes viejos sin el campo siguen funcionando. El cliente nunca envía
/// contexto de plan (se resuelve server-side).
/// </summary>
public sealed record LogNutritionRequest(
    MealCode MealCode,
    DateOnly? LocalDate,
    NutritionIntakePayload? Intake = null
);

/// <summary>
/// Payload de <c>PUT /api/v1/program/nutrition/log/{mealCode}</c>: el código
/// viaja en la ruta; el body lleva fecha opcional e intake de reemplazo
/// completo (null limpia los valores).
/// </summary>
public sealed record UpdateNutritionIntakeRequest(
    DateOnly? LocalDate,
    NutritionIntakePayload? Intake = null
);

/// <summary>
/// Payload de <c>PUT /api/v1/program/me/league-preferences</c> (LEAGUE v1):
/// <c>nickname</c> (seudónimo público; null o ausente = LIMPIA el valor
/// almacenado) y <c>optIn</c> (default OFF). Con <c>optIn = true</c> el
/// nickname es obligatorio (3-32 caracteres, charset acotado, tokens
/// reservados rechazados — validación FluentValidation → 400). El paciente
/// se resuelve del JWT, nunca del body.
/// </summary>
public sealed record UpdateLeaguePreferencesRequest(string? Nickname, bool OptIn);
