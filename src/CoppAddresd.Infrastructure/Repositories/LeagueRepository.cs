using CoppAddresd.Application.Features.ProgramProgress.DTOs.League;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>
/// Repositorio de la liga del paciente (LEAGUE v1): preferencias en
/// <c>patient_profiles</c> y cohorte de participantes con inscripción activa
/// + opt-in. Clase enfocada de SOLO LECTURA (más el update mínimo de las dos
/// columnas de la liga): no crece <see cref="ProgramRepository"/> (~9k
/// líneas). Valores SIEMPRE persistidos — nunca dispara recálculo de
/// puntajes por participante.
/// </summary>
public sealed class LeagueRepository(AppDbContext dbContext) : ILeagueRepository
{
    /// <summary>
    /// Contexto de la liga del paciente: preferencias, estado resoluble
    /// (city → state.code, MISMO mapeo que la biometría en
    /// <c>ListBiometriaPatientsAsync</c> — patient_profiles.city_id →
    /// cities.state_id → states.code) e inscripción activa. Null si el
    /// perfil no existe o no hay inscripción activa.
    /// </summary>
    public async Task<LeagueContext?> GetLeagueContextAsync(
        Guid patientId,
        CancellationToken ct = default
    )
    {
        var row = await dbContext
            .PatientProfiles.AsNoTracking()
            .Where(p => p.Id == patientId && p.DeletedAt == null)
            .Select(
                p =>
                    new
                    {
                        p.Id,
                        p.LeagueOptIn,
                        p.LeagueNickname,
                        StateCode =
                            p.City != null && p.City.State != null ? p.City.State.Code : null,
                        HasActiveEnrollment = dbContext.ProgramEnrollments.Any(
                            e => e.PatientId == p.Id && e.Status == ProgramEnrollmentStatus.Active
                        ),
                    }
            )
            .FirstOrDefaultAsync(ct);

        if (row is null || !row.HasActiveEnrollment)
        {
            return null;
        }

        return new LeagueContext(row.Id, row.LeagueOptIn, row.LeagueNickname, row.StateCode);
    }

    /// <summary>
    /// Filas del cohorte: opt-in + sin soft-delete + inscripción ACTIVA.
    /// <paramref name="stateCode"/> null → nacional (todos los estados).
    /// Una sola query set-based: las subqueries correlacionadas (racha por
    /// inscripción, evo por transformation_scores, adherencia/clínica por
    /// health_scores) se traducen a subconsultas/LATERAL — sin N+1.
    /// </summary>
    public async Task<IReadOnlyList<LeagueParticipantRow>> GetLeagueCohortRowsAsync(
        string? stateCode,
        CancellationToken ct = default
    )
    {
        var query = dbContext
            .PatientProfiles.AsNoTracking()
            .Where(p => p.LeagueOptIn && p.DeletedAt == null)
            .Join(
                dbContext
                    .ProgramEnrollments.AsNoTracking()
                    .Where(e => e.Status == ProgramEnrollmentStatus.Active),
                p => p.Id,
                e => e.PatientId,
                (p, e) => new { p, e }
            );

        if (!string.IsNullOrWhiteSpace(stateCode))
        {
            var code = stateCode.Trim().ToUpperInvariant();
            query = query.Where(
                x => x.p.City != null && x.p.City.State != null && x.p.City.State.Code == code
            );
        }

        var rows = await query
            .Select(
                x =>
                    new LeagueParticipantRow(
                        x.p.Id,
                        x.p.LeagueNickname,
                        // Racha: streak_states de la inscripción activa.
                        dbContext
                            .StreakStates.Where(s => s.EnrollmentId == x.e.Id)
                            .Select(s => (int?)s.CurrentStreak)
                            .FirstOrDefault(),
                        // Evolución: transformation_scores de la semana más reciente
                        // (semántica de período; orden alineado con el índice
                        // ix_transformation_scores_patient_week).
                        dbContext
                            .TransformationScores.Where(t => t.PatientId == x.p.Id)
                            .OrderByDescending(t => t.WeekNumber)
                            .ThenByDescending(t => t.CalculatedAt)
                            .Select(t => (int?)t.Score)
                            .FirstOrDefault(),
                        // Adherencia: health_scores del período más reciente.
                        dbContext
                            .HealthScores.Where(h => h.PatientId == x.p.Id)
                            .OrderByDescending(h => h.PeriodEnd)
                            .ThenByDescending(h => h.CalculatedAt)
                            .Select(h => (int?)h.ScoreAdherence)
                            .FirstOrDefault(),
                        // Clínica: mismo health_scores reciente.
                        dbContext
                            .HealthScores.Where(h => h.PatientId == x.p.Id)
                            .OrderByDescending(h => h.PeriodEnd)
                            .ThenByDescending(h => h.CalculatedAt)
                            .Select(h => (int?)h.ScoreClinical)
                            .FirstOrDefault()
                    )
            )
            .ToListAsync(ct);

        return rows;
    }

    /// <summary>
    /// Persiste SOLO <c>league_nickname</c> y <c>league_opt_in</c> (las demás
    /// columnas del perfil no se tocan). El nickname se almacena recortado;
    /// null o vacío limpia el valor almacenado. Perfil inexistente o borrado
    /// concurrentemente → 404.
    ///
    /// Nota de auditoría: ExecuteUpdate standalone (sin transacción
    /// explícita) → el trigger de <c>audit.activity_logs</c> registra la fila
    /// con <c>actor_type = 'SYSTEM'</c> y <c>user_id = NULL</c> (el
    /// interceptor GUC solo propaga el actor dentro de transacciones
    /// explícitas). Trait pre-existente del patrón — precedente:
    /// <c>PatientRepository.SoftDeleteAsync</c>.
    /// </summary>
    public async Task<LeaguePreferencesDto> UpdateLeaguePreferencesAsync(
        Guid patientId,
        string? nickname,
        bool optIn,
        CancellationToken ct = default
    )
    {
        var exists = await dbContext
            .PatientProfiles.AsNoTracking()
            .AnyAsync(p => p.Id == patientId && p.DeletedAt == null, ct);
        if (!exists)
        {
            throw new NotFoundException($"Paciente {patientId} no encontrado.");
        }

        var storedNickname = string.IsNullOrWhiteSpace(nickname) ? null : nickname.Trim();
        // WHERE con DeletedAt == null: cierra la carrera check-then-update del
        // AnyAsync previo (un borrado concurrente → 0 filas → 404, sin tocar
        // un perfil eliminado).
        var affected = await dbContext
            .PatientProfiles.Where(p => p.Id == patientId && p.DeletedAt == null)
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(p => p.LeagueNickname, storedNickname)
                        .SetProperty(p => p.LeagueOptIn, optIn),
                ct
            );
        if (affected == 0)
        {
            throw new NotFoundException($"Paciente {patientId} no encontrado.");
        }

        return new LeaguePreferencesDto(optIn, storedNickname);
    }

    /// <summary>
    /// Estado resoluble del paciente (city → state.code), sin exigir
    /// inscripción activa ni opt-in: lo consume el command de preferencias
    /// para invalidar el cohorte cacheado del paciente al cambiar opt-in/
    /// nickname (revocación de consentimiento inmediata).
    /// </summary>
    public async Task<string?> ResolveStateCodeAsync(
        Guid patientId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .PatientProfiles.AsNoTracking()
            .Where(p => p.Id == patientId && p.DeletedAt == null)
            .Select(p => p.City != null && p.City.State != null ? p.City.State.Code : null)
            .FirstOrDefaultAsync(ct);
}