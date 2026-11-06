using CoppAddresd.Application.Features.Nutrition.Cases;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Nutrition;

public sealed class NutritionDraftGenerationTests
{
    [Theory]
    [InlineData("pending")] [InlineData("generating")] [InlineData("ready")]
    public async Task Request_UsesCorrelatedBatteryAndToken_WithoutGeneratingContent(string state)
    {
        var access = Substitute.For<INutritionCaseStore>();
        var assessments = Substitute.For<IProgramAssignmentRepository>();
        var reservations = Substitute.For<INutritionDraftReservationStore>();
        var patient = Guid.NewGuid(); var battery = Guid.NewGuid(); var plan = Guid.NewGuid();
        var actor = new NutritionActor(Guid.NewGuid(), null, true, true, false);
        access.CanAccessAsync(patient, actor, Arg.Any<CancellationToken>()).Returns(true);
        access.GetContextAsync(patient, Arg.Any<CancellationToken>()).Returns(new NutritionCaseContext(null,null,"UTC",true,battery));
        assessments.FindCompletedInitialBatteryAsync(patient,battery,Arg.Any<CancellationToken>()).Returns(battery);
        var reservation = new NutritionDraftReservation { State=state, PrescriptionId=state=="ready" ? plan : null };
        reservations.RequestAsync(patient,battery,Arg.Any<DateTime>(),Arg.Any<CancellationToken>()).Returns(reservation);
        using var cancel = new CancellationTokenSource();
        var result = await new NutritionDraftGenerationCommandHandler(access,assessments,reservations,TimeProvider.System)
            .Handle(new(patient,actor),cancel.Token);
        Assert.Equal(reservation.Id,result.ReservationId); Assert.Equal(state,result.State);
        Assert.Equal(reservation.PrescriptionId,result.PrescriptionId);
        await reservations.Received(1).RequestAsync(patient,battery,Arg.Any<DateTime>(),cancel.Token);
    }

    [Theory]
    [InlineData(false, true)] [InlineData(true, false)]
    public async Task Request_RejectsForeignScopeOrMissingBattery(bool visible, bool batteryPresent)
    {
        var access=Substitute.For<INutritionCaseStore>(); var assessments=Substitute.For<IProgramAssignmentRepository>();
        var reservations=Substitute.For<INutritionDraftReservationStore>();
        var actor=new NutritionActor(Guid.NewGuid(),null,true,true,false); var patient=Guid.NewGuid();
        access.CanAccessAsync(patient,actor,Arg.Any<CancellationToken>()).Returns(visible);
        access.GetContextAsync(patient,Arg.Any<CancellationToken>()).Returns(new NutritionCaseContext(null,null,"UTC",false,batteryPresent ? Guid.NewGuid() : null));
        var handler=new NutritionDraftGenerationCommandHandler(access,assessments,reservations,TimeProvider.System);
        if (!visible) await Assert.ThrowsAsync<NotFoundException>(()=>handler.Handle(new(patient,actor),CancellationToken.None));
        else await Assert.ThrowsAsync<UnprocessableEntityException>(()=>handler.Handle(new(patient,actor),CancellationToken.None));
        Assert.Empty(reservations.ReceivedCalls());
    }
}
