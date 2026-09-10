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
}