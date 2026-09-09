using CoppAddresd.Application.Services.ProgramProgress;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Repositorio del envío de recordatorios de hito del programa
/// (<c>program_milestone_sends</c>): candidatos activos con usuario para el
/// job + reclamo/estado del par (enrollment, milestone_day). Repositorio
/// enfocado (precedente: DeviceTokenRepository/LeagueRepository) — no crece
/// IProgramRepository.
/// </summary>
public interface IProgramMilestoneRepository
{
    /// <summary>
    /// Candidatos a recordatorio: inscripciones Activas con fecha de inicio
    /// posterior al corte (job, 95 días) cuyo perfil de paciente tiene usuario
    /// de <c>auth.users</c> (sin usuario no hay push ni chat).
    /// </summary>
    Task<IReadOnlyList<MilestoneEnrollmentCandidate>> ListActiveCandidatesAsync(
        DateOnly startLocalDateCutoff, CancellationToken ct = default);

    /// <summary>
    /// Registro de envío del par (enrollmentId, milestoneDay), o null si el
    /// hito todavía no se reclamó.
    /// </summary>
    Task<ProgramMilestoneSend?> GetSendAsync(
        Guid enrollmentId, int milestoneDay, CancellationToken ct = default);

    /// <summary>
    /// Inserta la reclamación Pending del hito (SaveChanges inmediato). Una
    /// violación del índice único (enrollment_id, milestone_day) propaga al
    /// llamador, que la trata como "ya reclamado por otra pasada".
    /// </summary>
    Task AddAsync(ProgramMilestoneSend send, CancellationToken ct = default);

    /// <summary>Persiste el estado final del envío (Sent/Failed/Skipped/reintento).</summary>
    Task UpdateAsync(ProgramMilestoneSend send, CancellationToken ct = default);

    /// <summary>
    /// Candidato de CUALQUIER inscripción (no solo Active) para forzar el envío
    /// de un hito desde las herramientas demo (<c>ProgramMilestoneSenderJob.ForceSendAsync</c>):
    /// se exige únicamente que el perfil del paciente tenga usuario
    /// (sin usuario no hay push ni chat). null si la inscripción no existe o su
    /// paciente no tiene cuenta.
    /// </summary>
    Task<MilestoneEnrollmentCandidate?> GetCandidateAsync(
        Guid enrollmentId, CancellationToken ct = default);

    /// <summary>
    /// Historial de envíos (<c>program_milestone_sends</c>) para demo/herramientas:
    /// ordenado por <c>CreatedAt</c> descendente, con filtros opcionales por
    /// inscripción, por paciente (join a <c>program_enrollments</c> vía
    /// <c>EnrollmentId</c>) y por estado, y tope de filas (default 100,
    /// máximo 500). Proyección <c>AsNoTracking</c> con la navegación
    /// <c>Enrollment</c> cargada (solo para exponer <c>PatientId</c> sin N+1).
    /// </summary>
    Task<IReadOnlyList<ProgramMilestoneSend>> ListSendsAsync(
        Guid? enrollmentId = null,
        Guid? patientId = null,
        ProgramMilestoneSendStatus? status = null,
        int? limit = null,
        CancellationToken ct = default);

    /// <summary>
    /// Elimina filas de envío para demo/herramientas (reset): las de una
    /// inscripción o TODAS si <paramref name="enrollmentId"/> es null. Devuelve
    /// la cantidad eliminada.
    /// </summary>
    Task<int> DeleteSendsAsync(Guid? enrollmentId = null, CancellationToken ct = default);
}
