using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Application.Services.ProgramProgress;

/// <summary>
/// Candidato a control de hito: inscripción activa de un paciente con cuenta
/// de usuario (UserId no null — sin usuario no hay push ni chat). Proyección
/// plana devuelta por <c>IProgramControlRepository</c> para no exponer
/// entidades trackeadas al job.
/// </summary>
public sealed record ProgramControlEnrollmentCandidate(
    Guid EnrollmentId,
    Guid PatientId,
    Guid UserId,
    string Timezone,
    DateOnly StartLocalDate);

/// <summary>
/// Control de la fase 2 vencido (follow-up, miss o no_upload_timeout) junto con
/// la zona horaria IANA de su inscripción: el job convierte el instante UTC
/// actual a la hora local del paciente antes de evaluar las funciones puras de
/// vencimiento (<see cref="ProgramControlSchedule"/>), que nunca comparan en
/// UTC del servidor.
/// </summary>
public sealed record ProgramControlDueItem(ProgramControl Control, string Timezone);

/// <summary>
/// Resultado de una pasada completa de <c>ProgramControlJob.RunAsync</c>
/// (endpoint demo <c>POST /program-controls/run</c>): totales de la pasada más
/// el detalle por par (inscripción, día) procesado. Los pares que ya estaban en
/// estado terminal (idempotencia) o que quedaron Pending por un fallo
/// transitorio reintentable NO aparecen en <c>Details</c> ni cuentan en los
/// totales (no fueron trabajo de esta pasada).
/// </summary>
public sealed record ProgramControlRunResult(
    int Candidates,
    int Sent,
    int Skipped,
    int Failed,
    IReadOnlyList<ProgramControlSendResult> Details);

/// <summary>
/// Resultado del envío de un par (inscripción, día de hito): estado final
/// persistido y el thread de chat donde se inyectó el mensaje proactivo
/// (null salvo en Sent).
/// </summary>
public sealed record ProgramControlSendResult(
    Guid EnrollmentId,
    int MilestoneDay,
    ProgramControlStatus Status,
    string? ThreadId);