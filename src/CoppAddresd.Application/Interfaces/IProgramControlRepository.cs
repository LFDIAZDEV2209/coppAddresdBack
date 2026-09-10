using CoppAddresd.Application.Services.ProgramProgress;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Repositorio del control conversacional del programa
/// (<c>program_controls</c>): candidatos activos con usuario para el job +
/// reclamo/estado del par (enrollment, milestone_day). Repositorio enfocado
/// (precedente: DeviceTokenRepository/LeagueRepository) — no crece
/// IProgramRepository.
/// </summary>
public interface IProgramControlRepository
{
    /// <summary>
    /// Candidatos a control: inscripciones Activas con fecha de inicio
    /// posterior al corte (job, 95 días) cuyo perfil de paciente tiene usuario
    /// de <c>auth.users</c> (sin usuario no hay push ni chat).
    /// </summary>
    Task<IReadOnlyList<ProgramControlEnrollmentCandidate>> ListActiveCandidatesAsync(
        DateOnly startLocalDateCutoff, CancellationToken ct = default);

    /// <summary>
    /// Registro del par (enrollmentId, milestoneDay), o null si el hito
    /// todavía no se reclamó.
    /// </summary>
    Task<ProgramControl?> GetAsync(
        Guid enrollmentId, int milestoneDay, CancellationToken ct = default);

    /// <summary>
    /// Inserta la reclamación Pending del hito (SaveChanges inmediato). Una
    /// violación del índice único (enrollment_id, milestone_day) propaga al
    /// llamador, que la trata como "ya reclamado por otra pasada".
    /// </summary>
    Task AddAsync(ProgramControl control, CancellationToken ct = default);

    /// <summary>Persiste el estado del control (Sent/Failed/Skipped/reintento).</summary>
    Task UpdateAsync(ProgramControl control, CancellationToken ct = default);

    /// <summary>
    /// Candidato de CUALQUIER inscripción (no solo Active) para forzar el envío
    /// de un hito desde las herramientas demo (<c>ProgramControlJob.ForceSendAsync</c>):
    /// se exige únicamente que el perfil del paciente tenga usuario
    /// (sin usuario no hay push ni chat). null si la inscripción no existe o su
    /// paciente no tiene cuenta.
    /// </summary>
    Task<ProgramControlEnrollmentCandidate?> GetCandidateAsync(
        Guid enrollmentId, CancellationToken ct = default);

    /// <summary>
    /// Historial de controles (<c>program_controls</c>) para demo/herramientas:
    /// ordenado por <c>CreatedAt</c> descendente, con filtros opcionales por
    /// inscripción, por paciente (join a <c>program_enrollments</c> vía
    /// <c>EnrollmentId</c>) y por estado, y tope de filas (default 100,
    /// máximo 500). Proyección <c>AsNoTracking</c> con la navegación
    /// <c>Enrollment</c> cargada (solo para exponer <c>PatientId</c> sin N+1).
    /// </summary>
    Task<IReadOnlyList<ProgramControl>> ListAsync(
        Guid? enrollmentId = null,
        Guid? patientId = null,
        ProgramControlStatus? status = null,
        int? limit = null,
        CancellationToken ct = default);

    /// <summary>
    /// Elimina filas de control para demo/herramientas (reset): las de una
    /// inscripción o TODAS si <paramref name="enrollmentId"/> es null. Devuelve
    /// la cantidad eliminada.
    /// </summary>
    Task<int> DeleteAsync(Guid? enrollmentId = null, CancellationToken ct = default);

    /// <summary>
    /// Control abierto más reciente del usuario autenticado (fase 2): resuelve
    /// auth-user → paciente con el MISMO mapeo de
    /// <c>UploadLabExamCommandHandler</c> (perfil de paciente con
    /// <c>user_id</c> igual al id del usuario y sin soft delete) y devuelve el
    /// control no terminal (Sent/Responded/FollowedUp) más nuevo por
    /// <c>sent_at</c> descendente. <paramref name="threadId"/> actúa como
    /// filtro de seguridad adicional cuando se conoce (el mensaje entrante debe
    /// pertenecer al thread del control). null si el usuario no tiene paciente
    /// o no hay control abierto.
    /// </summary>
    Task<ProgramControl?> FindOpenControlForUserAsync(
        Guid authUserId, string? threadId = null, CancellationToken ct = default);

    /// <summary>
    /// Transición guardada Sent|FollowedUp → Responded (el paciente escribió en
    /// el control abierto). UPDATE atómico con guarda de estado: devuelve true
    /// solo si la fila existía y estaba en un estado de origen válido.
    /// </summary>
    Task<bool> MarkRespondedAsync(Guid id, DateTime respondedAt, CancellationToken ct = default);

    /// <summary>
    /// Transición guardada Sent|Responded|FollowedUp → Completed (subida de
    /// examen asociada). Fija <c>completed_at</c> y <c>exam_batch_id</c>.
    /// Devuelve true solo si la fila existía y estaba en un estado de origen
    /// válido.
    /// </summary>
    Task<bool> MarkCompletedAsync(
        Guid id, Guid examBatchId, DateTime completedAt, CancellationToken ct = default);

    /// <summary>
    /// Transición guardada Sent|Responded|FollowedUp → ClosedWithoutExam por
    /// rechazo explícito del paciente (<c>closed_reason='declined'</c>).
    /// Devuelve true solo si la fila existía y estaba en un estado de origen
    /// válido.
    /// </summary>
    Task<bool> MarkClosedDeclinedAsync(Guid id, DateTime closedAt, CancellationToken ct = default);

    /// <summary>
    /// Transición guardada Sent → FollowedUp (follow-up de la fase 2 enviado).
    /// Fija <c>followup_sent_at</c>. Devuelve true solo si la fila existía y
    /// estaba en Sent (un control ya respondido jamás recibe follow-up).
    /// </summary>
    Task<bool> MarkFollowedUpAsync(Guid id, DateTime followupSentAt, CancellationToken ct = default);

    /// <summary>
    /// Transición guardada FollowedUp → Missed (sin respuesta tras el
    /// follow-up). Devuelve true solo si la fila existía y estaba en FollowedUp.
    /// </summary>
    Task<bool> MarkMissedAsync(Guid id, DateTime missedAt, CancellationToken ct = default);

    /// <summary>
    /// Transición guardada Responded → ClosedWithoutExam por vencimiento de la
    /// ventana de subida (<c>closed_reason='no_upload_timeout'</c>). Devuelve
    /// true solo si la fila existía y estaba en Responded.
    /// </summary>
    Task<bool> MarkNoUploadTimeoutAsync(Guid id, DateTime closedAt, CancellationToken ct = default);

    /// <summary>
    /// Controles vencidos para follow-up (fase 2): status Sent, sin follow-up
    /// previo y con <c>sent_at</c> anterior a <c>utcNow - followupHours</c>.
    /// Consulta gruesa en la base; el filtro fino (ventana 9–21 local) lo
    /// aplica el job con <see cref="ProgramControlSchedule.IsFollowupDue"/>.
    /// Incluye la zona horaria IANA de la inscripción, ordena por <c>sent_at</c>
    /// ascendente (los más antiguos primero) y limita a
    /// <paramref name="limit"/> filas (1..500, default 100).
    /// </summary>
    Task<IReadOnlyList<ProgramControlDueItem>> ListDueForFollowupAsync(
        DateTime utcNow, int followupHours, int limit = 100, CancellationToken ct = default);

    /// <summary>
    /// Controles vencidos para cierre por Missed (fase 2): status FollowedUp con
    /// <c>followup_sent_at</c> anterior a <c>utcNow - missAfterFollowupHours</c>.
    /// Consulta gruesa en la base; el job afina con
    /// <see cref="ProgramControlSchedule.IsMissDue"/> (sin ventana de entrega).
    /// Incluye la zona horaria IANA de la inscripción, ordena por
    /// <c>followup_sent_at</c> ascendente y limita a
    /// <paramref name="limit"/> filas (1..500, default 100).
    /// </summary>
    Task<IReadOnlyList<ProgramControlDueItem>> ListDueForMissAsync(
        DateTime utcNow, int missAfterFollowupHours, int limit = 100, CancellationToken ct = default);

    /// <summary>
    /// Controles respondidos vencidos por no subida de examen (fase 2): status
    /// Responded con <c>responded_at</c> anterior a
    /// <c>utcNow - noUploadCloseHours</c> (el job convierte
    /// <c>NoUploadCloseDays</c> a horas). Consulta gruesa en la base; el job
    /// afina con <see cref="ProgramControlSchedule.IsNoUploadTimeoutDue"/> (sin
    /// ventana de entrega). Incluye la zona horaria IANA de la inscripción,
    /// ordena por <c>responded_at</c> ascendente y limita a
    /// <paramref name="limit"/> filas (1..500, default 100).
    /// </summary>
    Task<IReadOnlyList<ProgramControlDueItem>> ListTimedOutNoUploadAsync(
        DateTime utcNow, int noUploadCloseHours, int limit = 100, CancellationToken ct = default);
}