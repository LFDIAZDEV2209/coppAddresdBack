using CoppAddresd.Api.Authorization;
using CoppAddresd.Api.Constants;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Services.ProgramProgress;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Api.Controllers;

/// <summary>
/// Endpoints demo/herramientas del control conversacional del programa
/// (Controles, <c>Program:Controls</c>): permiten correr el job a demanda,
/// FORZAR el envío de un hito para una inscripción real (sin esperar la
/// ventana 9–21 local ni el tick), inspeccionar el historial de
/// <c>app.program_controls</c>, resetearlo y ver las plantillas configuradas.
/// Pensados para demostraciones y diagnóstico — el envío programado sigue
/// gobernado por <c>ProgramControlHostedService</c>. Mutaciones
/// (run/force/reset) requieren <c>Program.ForceComplete</c>; lecturas,
/// <c>Program.View</c>. Deliberadamente sin MediatR: son llamadas delgadas a
/// servicios de aplicación ya registrados en DI (precedente de tooling:
/// FoodAiController/AgentsController resuelven servicios directo).
/// </summary>
[ApiController]
[Route("api/v1/program-controls")]
[Authorize]
public sealed class ProgramControlsController(
    ProgramControlJob job,
    IProgramControlRepository repository,
    IOptions<ProgramControlSettings> settings,
    ILogger<ProgramControlsController> logger) : ControllerBase
{
    /// <summary>
    /// Corre una pasada del job ahora mismo (demo: "ejecutá el job"). Devuelve
    /// los totales de la pasada (<c>candidates</c> = inscripciones activas
    /// consideradas; <c>sent</c>/<c>skipped</c>/<c>failed</c> = pares
    /// procesados en ESTA pasada) más el detalle por par con su estado final.
    /// Idempotente: los pares ya notificados en pasadas previas no se
    /// re-envían y no cuentan en los totales.
    /// </summary>
    [HttpPost("run")]
    [RequirePermission(PermissionCodes.ProgramForceComplete)]
    [ProducesResponseType(typeof(ProgramControlRunResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProgramControlRunResult>> Run(CancellationToken ct)
    {
        var result = await job.RunAsync(ct);
        logger.LogInformation(
            "Program.ControlsDemo: pasada manual. candidates={Candidates} sent={Sent} skipped={Skipped} failed={Failed}",
            result.Candidates, result.Sent, result.Skipped, result.Failed);
        return Ok(result);
    }

    /// <summary>
    /// FUERZA el envío del recordatorio del hito <c>milestoneDay</c> para la
    /// inscripción <c>enrollmentId</c> (demo: enviar el mensaje del día 7 de un
    /// paciente REAL ahora mismo). Ignora la ventana de entrega y el calendario
    /// — corre el mismo pipeline idempotente del job: si el par ya está
    /// Sent/Skipped/Failed devuelve su estado actual sin re-notificar; un día
    /// sin plantilla configurada queda Skipped. Inscripción inexistente o sin
    /// usuario de paciente → 404.
    /// </summary>
    [HttpPost("force")]
    [RequirePermission(PermissionCodes.ProgramForceComplete)]
    [ProducesResponseType(typeof(ProgramControlSendResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProgramControlSendResult>> ForceSend(
        [FromBody] ForceProgramControlSendRequest request,
        CancellationToken ct)
    {
        if (request.EnrollmentId == Guid.Empty)
        {
            return BadRequest(new { message = "El enrollmentId es requerido." });
        }

        if (request.MilestoneDay <= 0)
        {
            return BadRequest(new { message = "El milestoneDay debe ser un día de programa positivo." });
        }

        var result = await job.ForceSendAsync(request.EnrollmentId, request.MilestoneDay, ct);
        if (result is null)
        {
            return NotFound(new
            {
                message = "Inscripción no encontrada o sin usuario de paciente asociado (no se puede notificar).",
            });
        }

        logger.LogInformation(
            "Program.ControlsDemo: force-send. enrollment={EnrollmentId} day={MilestoneDay} status={Status}",
            result.EnrollmentId, result.MilestoneDay, result.Status);
        return Ok(result);
    }

    /// <summary>
    /// Historial de controles del programa (<c>app.program_controls</c>),
    /// ordenado por creación descendente (demo/ERP). Filtros opcionales por
    /// inscripción, paciente y estado; <c>limit</c> default 100, máximo 500.
    /// </summary>
    [HttpGet("list")]
    [RequirePermission(PermissionCodes.ProgramView)]
    [ProducesResponseType(typeof(IReadOnlyList<ProgramControlSendDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProgramControlSendDto>>> List(
        [FromQuery] Guid? enrollmentId,
        [FromQuery] Guid? patientId,
        [FromQuery] ProgramControlStatus? status,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        var controls = await repository.ListAsync(enrollmentId, patientId, status, limit, ct);
        return Ok(controls.Select(ProgramControlSendDto.FromEntity).ToList());
    }

    /// <summary>
    /// Reset del historial de controles para demo: elimina los registros de la
    /// inscripción indicada o TODOS si se omite <c>enrollmentId</c>. Devuelve
    /// la cantidad eliminada. Pensado para re-demostrar el ciclo completo
    /// (force-send → listar → reset).
    /// </summary>
    [HttpDelete("list")]
    [RequirePermission(PermissionCodes.ProgramForceComplete)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete([FromQuery] Guid? enrollmentId, CancellationToken ct)
    {
        var deleted = await repository.DeleteAsync(enrollmentId, ct);
        logger.LogInformation(
            "Program.ControlsDemo: reset de controles. enrollment={EnrollmentId} deleted={Deleted}",
            enrollmentId, deleted);
        return Ok(new { deleted });
    }

    /// <summary>
    /// Configuración vigente de los controles para demo/visibilidad
    /// (<c>Program:Controls</c>): días de hito, ventana, gracia, reintentos y
    /// las plantillas por día (título, mensaje, agente).
    /// </summary>
    [HttpGet("templates")]
    [RequirePermission(PermissionCodes.ProgramView)]
    [ProducesResponseType(typeof(ProgramControlTemplatesView), StatusCodes.Status200OK)]
    public ActionResult<ProgramControlTemplatesView> GetTemplates()
    {
        var s = settings.Value;
        return Ok(new ProgramControlTemplatesView(
            s.Enabled,
            s.Days.ToList(),
            s.StartLocalHour,
            s.EndLocalHour,
            s.GraceDays,
            s.MaxAttempts,
            s.Templates
                .OrderBy(t => t.Day)
                .Select(t => new ProgramControlTemplateView(t.Day, t.Title, t.Message, t.AgentTypeId))
                .ToList()));
    }
}

/// <summary>
/// Fila del historial de controles (GET /list) con el paciente resuelto por
/// join. Expone el ciclo de vida completo de la fase 2 (respuesta, follow-up,
/// completación, cierre sin examen y lote de examen asociado) además de los
/// campos de la fase 1.
/// </summary>
public sealed record ProgramControlSendDto(
    Guid Id,
    Guid EnrollmentId,
    Guid PatientId,
    int MilestoneDay,
    ProgramControlStatus Status,
    int Attempts,
    string? ThreadId,
    DateTime? SentAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    Guid? ExamBatchId,
    DateTime? RespondedAt,
    DateTime? FollowupSentAt,
    DateTime? CompletedAt,
    string? ClosedReason)
{
    /// <summary>
    /// Mapea la entidad (con la navegación <c>Enrollment</c> cargada por el
    /// repositorio) a la fila expuesta; la FK enrollment_id no es nullable, así
    /// que la navegación siempre viene poblada.
    /// </summary>
    public static ProgramControlSendDto FromEntity(ProgramControl control)
        => new(
            control.Id,
            control.EnrollmentId,
            control.Enrollment!.PatientId,
            control.MilestoneDay,
            control.Status,
            control.Attempts,
            control.ThreadId,
            control.SentAt,
            control.CreatedAt,
            control.UpdatedAt,
            control.ExamBatchId,
            control.RespondedAt,
            control.FollowupSentAt,
            control.CompletedAt,
            control.ClosedReason);
}

/// <summary>Plantilla configurada por día (GET /templates).</summary>
public sealed record ProgramControlTemplateView(int Day, string Title, string Message, string AgentTypeId);

/// <summary>Vista de configuración vigente de los controles (GET /templates).</summary>
public sealed record ProgramControlTemplatesView(
    bool Enabled,
    IReadOnlyList<int> Days,
    int StartLocalHour,
    int EndLocalHour,
    int GraceDays,
    int MaxAttempts,
    IReadOnlyList<ProgramControlTemplateView> Templates);

/// <summary>Payload de <c>POST /force</c>: inscripción + día de hito a forzar.</summary>
public sealed record ForceProgramControlSendRequest(Guid EnrollmentId, int MilestoneDay);