using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Patients;

/// <summary>
/// Estadísticas del directorio de pacientes: total, activos, nuevos en el
/// mes actual y sin profesional asignado. <paramref name="ClinicId"/> aplica
/// la frontera de clínica activa y <paramref name="OwnProfessionalId"/>
/// restringe al alcance "propio" del profesional (solo pacientes asignados a
/// él); ambos se resuelven en el backend desde el contexto/JWT, nunca del
/// cliente — misma semántica que <see cref="ListPatientsQuery"/>.
/// </summary>
public record GetPatientsStatsQuery(Guid? ClinicId = null, Guid? OwnProfessionalId = null)
    : IRequest<PatientStatsDto>;

/// <summary>
/// Cache-aside con TTL 30-60s (jitter anti-stampede) y hash de alcance en la
/// clave: un admin global y un profesional con alcance "propio" nunca
/// comparten key. Staleness máximo = TTL, tolerado por diseño para el
/// dashboard (el listado siempre lee datos frescos).
/// </summary>
public sealed class GetPatientsStatsQueryHandler(IPatientRepository repository, ICacheService cache)
    : IRequestHandler<GetPatientsStatsQuery, PatientStatsDto>
{
    public async Task<PatientStatsDto> Handle(GetPatientsStatsQuery request, CancellationToken ct)
    {
        var scopeHash = CacheKeys.HashScope(
            request.ClinicId?.ToString(),
            request.OwnProfessionalId?.ToString()
        );

        return await cache.GetOrCreateAsync(
            CacheKeys.Stats("patients", scopeHash),
            CacheKeys.StatsTtl(),
            async token =>
            {
                var now = DateTime.UtcNow;
                var monthStartUtc = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

                return await repository.GetStatsAsync(
                    request.ClinicId,
                    request.OwnProfessionalId,
                    monthStartUtc,
                    token
                );
            },
            ct
        );
    }
}
