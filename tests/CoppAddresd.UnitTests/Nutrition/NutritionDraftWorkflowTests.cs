using CoppAddresd.Application.Features.HealthTests.Events;
using CoppAddresd.Application.Features.Nutrition.Cases;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace CoppAddresd.UnitTests.Nutrition;

public sealed class NutritionDraftWorkflowTests
{
    [Fact]
    public async Task BatteryConsumer_ReservesExactCorrelation_AndPropagatesToken()
    {
        var workflow = Substitute.For<INutritionDraftWorkflow>();
        var notification = new BatteryCompletedEvent(Guid.NewGuid(), Guid.NewGuid());
        using var cancellation = new CancellationTokenSource();
        await new NutritionBatteryCompletedHandler(workflow).Handle(notification, cancellation.Token);
        await workflow.Received(1).QueueAsync(notification.PatientId, notification.BatteryAssignmentId, cancellation.Token);
    }

    [Fact]
    public void SystemActor_ScopeRestoresIdentity_AndCannotApprove()
    {
        var context = new HttpAuditActorContext(new HttpContextAccessor());
        Assert.False(NutritionSystemActor.IsActive);
        using (NutritionSystemActor.Begin())
        {
            Assert.True(NutritionSystemActor.IsActive);
            Assert.Equal(AuditActorType.System, context.ActorType);
            Assert.Null(context.UserId);
            Assert.Equal("system:nutrition", context.UserRole);
            Assert.False(NutritionSystemActor.Actor.CanApprove);
        }
        Assert.False(NutritionSystemActor.IsActive);
        Assert.Null(context.UserRole);
    }
}
