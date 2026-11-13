using CoppAddresd.Infrastructure.Repositories;
using Xunit;

namespace CoppAddresd.IntegrationTests;

public sealed partial class NutritionPlanWorkflowTests
{
    [NutritionPostgresFact]
    public async Task OverviewCycleHeaders_OnlyRequestedPatientApprovedHistory_WithoutMealGraph()
    {
        await using var db = Context(); var own = await PatientAsync(db); var foreign = await PatientAsync(db);
        var first = InboxPlan(own.Id, InboxNow.AddDays(-30)); PublishInboxPlan(first, new(2026, 11, 4));
        var current = InboxPlan(own.Id, InboxNow); current.Version = 2; PublishInboxPlan(current, new(2026, 12, 4));
        var version = InboxPlan(own.Id, InboxNow.AddDays(-1)); PublishInboxPlan(version, new(2026, 12, 4));
        version.CycleId = current.CycleId; version.IsPublished = false;
        var other = InboxPlan(foreign.Id, InboxNow); PublishInboxPlan(other, new(2026, 12, 4));
        var draft = InboxPlan(own.Id, InboxNow); draft.StartDate = new(2026, 12, 5); draft.EndDate = new(2027, 1, 3);
        db.NutritionPrescriptions.AddRange(first, current, version, other, draft); await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var headers = await new NutritionPrescriptionRepository(db).ListCycleHeadersAsync(own.Id, CancellationToken.None);
        Assert.Equal(3, headers.Count); Assert.Equal(2, headers.Select(h => h.CycleId).Distinct().Count());
        Assert.DoesNotContain(headers, h => h.PrescriptionId == other.Id || h.PrescriptionId == draft.Id);
        Assert.Empty(db.ChangeTracker.Entries());
    }
}
