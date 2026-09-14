using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Patients;

/// <summary>
/// Agregados del dashboard general de pacientes: KPIs (reutiliza la consulta de
/// stats), demografía, crecimiento mensual, top de profesionales y
/// distribución geográfica por estado. <paramref name="ClinicId"/> aplica la
/// frontera de clínica activa y <paramref name="OwnProfessionalId"/> restringe
/// al alcance "propio" (top de profesionales solo en alcance global);
/// <paramref name="StateCode"/> acota demografía, crecimiento y top de
/// profesionales a un estado de EE. UU. (la distribución geográfica siempre
/// viaja completa para permitir cambiar de selección). Misma semántica de
/// alcance que <see cref="ListPatientsQuery"/>.
/// </summary>
public record GetPatientsDashboardQuery(
    Guid? ClinicId = null,
    Guid? OwnProfessionalId = null,
    string? StateCode = null,
    int Months = 12
) : IRequest<PatientDashboardDto>;

/// <summary>
/// Cache-aside con el TTL de stats (30-60 s con jitter): el hash incluye
/// clínica + alcance propio + estado + rango de meses, de modo que dos
/// usuarios con distinto alcance nunca comparten clave. Staleness máximo =
/// TTL, tolerado por diseño para agregados de dashboard.
/// </summary>
public sealed class GetPatientsDashboardQueryHandler(
    IPatientDashboardRepository repository,
    ICacheService cache
) : IRequestHandler<GetPatientsDashboardQuery, PatientDashboardDto>
{
    public async Task<PatientDashboardDto> Handle(
        GetPatientsDashboardQuery request,
        CancellationToken ct
    )
    {
        var months = request.Months == 6 ? 6 : 12;
        var stateCode = string.IsNullOrWhiteSpace(request.StateCode)
            ? null
            : request.StateCode.Trim().ToUpperInvariant();

        var scopeHash = CacheKeys.HashScope(
            request.ClinicId?.ToString(),
            request.OwnProfessionalId?.ToString(),
            stateCode ?? "all",
            $"{months}m"
        );

        return await cache.GetOrCreateAsync(
            CacheKeys.Stats("patients-dashboard", scopeHash),
            CacheKeys.StatsTtl(),
            async token =>
                await repository.GetDashboardAsync(
                    request.ClinicId,
                    request.OwnProfessionalId,
                    stateCode,
                    months,
                    DateTime.UtcNow,
                    token
                ),
            ct
        );
    }
}

/// <summary>Rebanada genérica del dashboard (valor + conteo).</summary>
public record PatientDashboardSlice(string? Value, int Count);

/// <summary>Rebanada con identidad + nombre (aseguradoras).</summary>
public record PatientDashboardNamedSlice(Guid? Id, string? Name, int Count);

/// <summary>Diagnóstico principal agregado del alcance.</summary>
public record PatientDashboardDiagnosisSlice(string Code, string? Description, int Count);

/// <summary>Punto mensual de nuevos pacientes (mes calendario UTC).</summary>
public record PatientDashboardMonthSlice(int Year, int Month, int Count);

/// <summary>Estado de EE. UU. con pacientes y porcentaje sobre el total con estado.</summary>
public record PatientDashboardStateSlice(string Code, string Name, int Count, decimal Percentage);

/// <summary>Profesional con más pacientes asignados activos (solo alcance global).</summary>
public record PatientDashboardProfessionalSlice(Guid Id, string Name, int Count);

/// <summary>
/// Contrato completo del dashboard: KPIs con el mismo alcance del directorio,
/// demografía real, crecimiento mensual, top de profesionales y distribución
/// geográfica. Colecciones vacías y porcentaje 0 cuando no hay datos — nunca
/// cifras ficticias.
/// </summary>
public record PatientDashboardDto(
    PatientStatsDto Kpis,
    IReadOnlyList<PatientDashboardSlice> GenderDistribution,
    IReadOnlyList<PatientDashboardSlice> AgeDistribution,
    IReadOnlyList<PatientDashboardSlice> MaritalStatusDistribution,
    IReadOnlyList<PatientDashboardNamedSlice> InsurerDistribution,
    IReadOnlyList<PatientDashboardDiagnosisSlice> TopDiagnoses,
    IReadOnlyList<PatientDashboardMonthSlice> NewPatientsByMonth,
    IReadOnlyList<PatientDashboardStateSlice> States,
    IReadOnlyList<PatientDashboardProfessionalSlice> TopProfessionals,
    decimal EmergencyContactPct
);
