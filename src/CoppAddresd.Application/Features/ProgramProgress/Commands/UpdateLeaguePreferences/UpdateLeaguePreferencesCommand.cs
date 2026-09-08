using CoppAddresd.Application.Features.ProgramProgress.DTOs.League;
using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.UpdateLeaguePreferences;

/// <summary>
/// Command de preferencias de la liga del paciente autenticado (LEAGUE v1):
/// opt-in (default OFF, privacidad por diseño) + nickname (seudónimo
/// público; requerido cuando optIn = true, 3-32 caracteres, charset acotado,
/// tokens reservados rechazados). El <c>patientId</c> SIEMPRE viene resuelto
/// del JWT por la capa API (nunca del body, anti-IDOR AC-11). Persiste SOLO
/// las dos columnas de la liga en <c>patient_profiles</c> (el nickname
/// sobrevive a re-inscripciones). Perfil inexistente → 404.
///
/// Al guardar, invalida el cohorte cacheado del paciente
/// (<c>league:{estado}:v1</c> y <c>league:ALL:v1</c>): la revocación del
/// opt-in debe ser INMEDIATA (privacidad). Las claves de OTROS pacientes
/// siguen viviendo su TTL (5 min) — sin invalidaciones fan-out.
/// </summary>
public sealed record UpdateLeaguePreferencesCommand(
    Guid PatientId,
    string? Nickname,
    bool OptIn) : IRequest<LeaguePreferencesDto>;

public sealed class UpdateLeaguePreferencesCommandHandler(
    ILeagueRepository repository,
    ICacheService cache) : IRequestHandler<UpdateLeaguePreferencesCommand, LeaguePreferencesDto>
{
    public async Task<LeaguePreferencesDto> Handle(
        UpdateLeaguePreferencesCommand request, CancellationToken ct)
    {
        var saved = await repository.UpdateLeaguePreferencesAsync(
            request.PatientId, request.Nickname, request.OptIn, ct);

        // Revocación de consentimiento inmediata: las claves del cohorte que
        // PODRÍAN contener a este paciente se invalidan (estado + nacional).
        // Sin estado resoluble → solo la clave ALL (que es la misma).
        var stateCode = await repository.ResolveStateCodeAsync(request.PatientId, ct);
        await cache.RemoveAsync(CacheKeys.League(stateCode), ct);
        if (stateCode is not null)
        {
            await cache.RemoveAsync(CacheKeys.League(null), ct);
        }

        return saved;
    }
}