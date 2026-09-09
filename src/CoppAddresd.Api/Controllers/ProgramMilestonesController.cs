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
/// Endpoints demo/herramientas del recordatorio proactivo de hitos del programa
/// (Program Milestone Reminder, <c>Program:MilestoneSender</c>): permiten correr
/// el job a demanda, FORZAR el envío de un hito para una inscripción real
/// (sin esperar la ventana 9–21 local ni el tick), inspeccionar el historial
/// de <c>app.program_milestone_sends</c>, resetearlo y ver las plantillas
/// configuradas. Pensados para demostraciones y diagnóstico — el envío
/// programado sigue gobernado por <c>ProgramMilestoneSenderHostedService</c>.
/// Mutaciones (run/force/reset) requieren <c>Program.ForceComplete</c>;
/// lecturas, <c>Program.View</c>. Deliberadamente sin MediatR: son llamadas
/// delgadas a servicios de aplicación ya registrados en DI (precedente de
/// tooling: FoodAiController/AgentsController resuelven servicios directo).
/// </summary>
[ApiController]
[Route("api/v1/program-milestones")]
[Authorize]
public sealed class ProgramMilestonesController(
    ProgramMilestoneSenderJob job,
    IProgramMilestoneRepository repository,
    IOptions<ProgramMilestoneSenderSettings> settings,
    ILogger<ProgramMilestonesController> logger) : ControllerBase
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
    [ProducesResponseType(typeof(ProgramMilestoneRunResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProgramMilestoneRunResult>> Run(CancellationToken ct)
    {
        var result = await job.RunAsync(ct);
        logger.LogInformation(
            "Program.MilestoneSenderDemo: pasada manual. candidates={Candidates} sent={Sent} skipped={Skipped} failed={Failed}",
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
    [ProducesResponseType(typeof(ProgramMilestoneSendResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProgramMilestoneSendResult>> ForceSend(
        [FromBody] ForceMilestoneSendRequest request,
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
            "Program.MilestoneSenderDemo: force-send. enrollment={EnrollmentId} day={MilestoneDay} status={Status}",
            result.EnrollmentId, result.MilestoneDay, result.Status);
        return Ok(result);
    }

    /// <summary>
    /// Historial de envíos de recordatorios de hito
    /// (<c>app.program_milestone_sends</c>), ordenado por creación descendente
    /// (demo/ERP). Filtros opcionales por inscripción, paciente y estado;
    /// <c>limit</c> default 100, máximo 500.
    /// </summary>
    [HttpGet("sends")]
    [RequirePermission(PermissionCodes.ProgramView)]
    [ProducesResponseType(typeof(IReadOnlyList<MilestoneSendDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MilestoneSendDto>>> ListSends(
        [FromQuery] Guid? enrollmentId,
        [FromQuery] Guid? patientId,
        [FromQuery] ProgramMilestoneSendStatus? status,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        var sends = await repository.ListSendsAsync(enrollmentId, patientId, status, limit, ct);
        return Ok(sends.Select(MilestoneSendDto.FromEntity).ToList());
    }

    /// <summary>
    /// Reset del historial de envíos para demo: elimina los registros de la
    /// inscripción indicada o TODOS si se omite <c>enrollmentId</c>. Devuelve
    /// la cantidad eliminada. Pensado para re-demostrar el ciclo completo
    /// (force-send → listar → reset).
    /// </summary>
    [HttpDelete("sends")]
    [RequirePermission(PermissionCodes.ProgramForceComplete)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteSends([FromQuery] Guid? enrollmentId, CancellationToken ct)
    {
        var deleted = await repository.DeleteSendsAsync(enrollmentId, ct);
        logger.LogInformation(
            "Program.MilestoneSenderDemo: reset de envíos. enrollment={EnrollmentId} deleted={Deleted}",
            enrollmentId, deleted);
        return Ok(new { deleted });
    }

    /// <summary>
    /// Configuración vigente del recordatorio de hitos para demo/visibilidad
    /// (<c>Program:MilestoneSender</c>): días de hito, ventana, gracia,
    /// reintentos y las plantillas por día (título, mensaje, agente).
    /// </summary>
    [HttpGet("templates")]
    [RequirePermission(PermissionCodes.ProgramView)]
    [ProducesResponseType(typeof(MilestoneTemplatesView), StatusCodes.Status200OK)]
    public ActionResult<MilestoneTemplatesView> GetTemplates()
    {
        var s = settings.Value;
        return Ok(new MilestoneTemplatesView(
            s.Enabled,
            s.Days.ToList(),
            s.StartLocalHour,
            s.EndLocalHour,
            s.GraceDays,
            s.MaxAttempts,
            s.Templates
                .OrderBy(t => t.Day)
                .Select(t => new MilestoneTemplateView(t.Day, t.Title, t.Message, t.AgentTypeId))
                .ToList()));
    }
}

/// <summary>Fila del historial de envíos (GET /sends) con el paciente resuelto por join.</summary>
public sealed record MilestoneSendDto(
    Guid Id,
    Guid EnrollmentId,
    Guid PatientId,
    int MilestoneDay,
    ProgramMilestoneSendStatus Status,
    int Attempts,
    string? ThreadId,
    DateTime? SentAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    /// <summary>
    /// Mapea la entidad (con la navegación <c>Enrollment</c> cargada por el
    /// repositorio) a la fila expuesta; la FK enrollment_id no es nullable, así
    /// que la navegación siempre viene poblada.
    /// </summary>
    public static MilestoneSendDto FromEntity(ProgramMilestoneSend send)
        => new(
            send.Id,
            send.EnrollmentId,
            send.Enrollment!.PatientId,
            send.MilestoneDay,
            send.Status,
            send.Attempts,
            send.ThreadId,
            send.SentAt,
            send.CreatedAt,
            send.UpdatedAt);
}

/// <summary>Plantilla configurada por día (GET /templates).</summary>
public sealed record MilestoneTemplateView(int Day, string Title, string Message, string AgentTypeId);

/// <summary>Vista de configuración vigente del recordatorio de hitos (GET /templates).</summary>
public sealed record MilestoneTemplatesView(
    bool Enabled,
    IReadOnlyList<int> Days,
    int StartLocalHour,
    int EndLocalHour,
    int GraceDays,
    int MaxAttempts,
    IReadOnlyList<MilestoneTemplateView> Templates);

/// <summary>Payload de <c>POST /force</c>: inscripción + día de hito a forzar.</summary>
public sealed record ForceMilestoneSendRequest(Guid EnrollmentId, int MilestoneDay);
