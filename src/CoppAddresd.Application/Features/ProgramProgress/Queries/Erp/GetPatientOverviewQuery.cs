using CoppAddresd.Application.Features.ProgramProgress.DTOs.Erp;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Features.Nutrition.Cases;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.Erp;

/// <summary>Perfil 360 de un paciente (SPEC §23, AC-54, anti-IDOR).</summary>
public sealed record GetPatientOverviewQuery(Guid PatientId) : IRequest<PatientOverviewDto?>;

public sealed class GetPatientOverviewQueryHandler(IProgramRepository repository,
    INutritionPrescriptionRepository nutrition, TimeProvider clock)
    : IRequestHandler<GetPatientOverviewQuery, PatientOverviewDto?>
{
    public async Task<PatientOverviewDto?> Handle(GetPatientOverviewQuery request, CancellationToken ct)
    {
        var overview = await repository.GetPatientOverviewAsync(request.PatientId, ct);
        if (overview is null) return null;
        PatientOverviewCurrentCycle? currentCycle = null;
        if (overview.Enrollment is { } enrollment)
        {
            var today = NutritionStateMachine.Today(clock.GetUtcNow(), enrollment.Timezone);
            var headers = await nutrition.ListCycleHeadersAsync(request.PatientId, ct);
            var current = headers.Where(h => h.IsPublished && h.StartDate <= today
                && h.EndDate >= today && h.StartDate > today.AddDays(-30))
                .OrderByDescending(h => h.StartDate).ThenByDescending(h => h.PrescriptionId).FirstOrDefault();
            if (current is not null)
            {
                // Una versión nueva no crea otro número; los ciclos publicados se numeran por inicio.
                var cycles = headers.GroupBy(h => h.CycleId)
                    .OrderBy(g => g.Min(h => h.StartDate)).ThenBy(g => g.Min(h => h.CreatedAt)).ThenBy(g => g.Key)
                    .Select(g => g.Key).ToArray();
                currentCycle = new(Array.IndexOf(cycles, current.CycleId) + 1,
                    today.DayNumber - current.StartDate.DayNumber + 1, 30, current.StartDate, current.EndDate);
            }
        }
        return overview with { ProgramName = overview.Enrollment?.TemplateName, CurrentCycle = currentCycle };
    }
}
