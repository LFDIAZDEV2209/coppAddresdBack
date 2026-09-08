using CoppAddresd.Application.Features.ProgramProgress.DTOs.League;
using CoppAddresd.Domain.Exceptions;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Lectura/escritura de la liga del paciente (LEAGUE v1): preferencias
/// (opt-in + nickname) y cohorte de participantes para el ranking. El
/// cohorte se lee SOLO de datos persistidos (nunca dispara recálculo de
/// puntajes por participante). La resolución del estado (stateCode) usa el
/// MISMO mapeo que la biometría (patient_profiles.city_id → cities.state_id
/// → states.code, ver ListBiometriaPatientsAsync): si el paciente no tiene
/// estado resoluble → cohorte nacional.
/// </summary>
public interface ILeagueRepository
{
    /// <summary>
    /// Contexto de la liga del paciente: preferencias + estado resoluble
    /// (city → state.code) + presencia de inscripción activa. Devuelve null
    /// si el perfil no existe o el paciente no tiene inscripción activa
    /// (el handler responde 404 NO_ACTIVE_ENROLLMENT, anti-IDOR AC-11).
    /// </summary>
    Task<LeagueContext?> GetLeagueContextAsync(Guid patientId, CancellationToken ct = default);

    /// <summary>
    /// Filas del cohorte de la liga: pacientes con <c>league_opt_in = true</c>,
    /// sin soft-delete y con inscripción ACTIVA. <paramref name="stateCode"/>
    /// null → cohorte nacional (todos los estados). Una sola query set-based
    /// (subqueries correlacionadas por participante: racha, evo, adherencia,
    /// clínica — sin N+1).
    /// </summary>
    Task<IReadOnlyList<LeagueParticipantRow>> GetLeagueCohortRowsAsync(
        string? stateCode, CancellationToken ct = default);

    /// <summary>
    /// Persiste SOLO las dos columnas de la liga (<c>league_nickname</c> y
    /// <c>league_opt_in</c>; el resto del perfil no se toca). El nickname se
    /// almacena recortado (trim). Perfil inexistente → 404
    /// (<see cref="NotFoundException"/>).
    /// </summary>
    Task<LeaguePreferencesDto> UpdateLeaguePreferencesAsync(
        Guid patientId, string? nickname, bool optIn, CancellationToken ct = default);

    /// <summary>
    /// Estado resoluble del paciente (city → state.code, MISMO mapeo que la
    /// lectura de la liga) SIN exigir inscripción activa ni opt-in: lo usa el
    /// command de preferencias para invalidar las claves de caché del
    /// paciente. Null cuando el perfil no existe o no tiene estado resoluble.
    /// </summary>
    Task<string?> ResolveStateCodeAsync(Guid patientId, CancellationToken ct = default);
}