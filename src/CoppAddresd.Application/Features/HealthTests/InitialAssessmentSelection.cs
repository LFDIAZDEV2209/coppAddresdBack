using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Application.Features.HealthTests;

/// <summary>El catálogo inicial y una sola cohorte definen el contador; nunca se recorta a nueve.</summary>
public static class InitialAssessmentSelection
{
    public static IReadOnlyList<HealthTestAssignment> Select(HealthTestBattery? battery,
        IReadOnlyList<HealthTestBatteryAssignment> cohorts, IReadOnlyList<HealthTestAssignment> assignments)
    {
        if (battery is null) return [];
        var current = cohorts
            .Where(x => x.BatteryId == battery.Id && x.Status is not (HealthTestAssignmentStatus.cancelled or HealthTestAssignmentStatus.expired))
            .OrderByDescending(x => x.AssignedAt).ThenByDescending(x => x.Id).FirstOrDefault();
        if (current is null) return [];
        return battery.Items.OrderBy(x => x.SortOrder)
            .Select(item => assignments.Where(a => a.BatteryAssignmentId == current.Id
                && a.Version?.InstrumentId == item.InstrumentId && (item.VersionId is null || item.VersionId == a.VersionId)
                && a.Status is HealthTestAssignmentStatus.pending or HealthTestAssignmentStatus.in_progress or HealthTestAssignmentStatus.completed)
                .OrderByDescending(a => a.Status == HealthTestAssignmentStatus.completed)
                .ThenByDescending(a => a.Status == HealthTestAssignmentStatus.in_progress)
                .ThenByDescending(a => a.AssignedAt).ThenByDescending(a => a.Id).FirstOrDefault())
            .OfType<HealthTestAssignment>().ToArray();
    }
}
