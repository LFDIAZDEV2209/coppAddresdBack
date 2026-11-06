using System.Text.Json;
using CoppAddresd.Application.Features.Nutrition.Cases;
using CoppAddresd.Application.Features.Nutrition.Engine;
using CoppAddresd.Application.Features.Nutrition.Queries;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;
using FluentValidation;
using NSubstitute;

namespace CoppAddresd.UnitTests.Nutrition;

public sealed class NutritionInboxQueryTests
{
    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 6, 2, 0, 0, TimeSpan.Zero);
    }
    private static NutritionActor Actor => new(Guid.NewGuid(), Guid.NewGuid(), false, true, false);

    [Theory]
    [InlineData(0,20,null)] [InlineData(1,0,null)] [InlineData(1,101,null)]
    [InlineData(1,20,"inexistente")] [InlineData(1,20,"")]
    public async Task InvalidFilters_RejectBeforeRepository(int page, int size, string? state)
    {
        var repository = Substitute.For<INutritionInboxRepository>();
        await Assert.ThrowsAsync<ValidationException>(() => new NutritionInboxQueryHandler(repository,new Clock())
            .Handle(new(Actor,page,size,state),CancellationToken.None));
        Assert.Empty(repository.ReceivedCalls());
    }

    [Fact]
    public async Task LongSearch_AndMissingPermission_RejectBeforeRepository()
    {
        var repository = Substitute.For<INutritionInboxRepository>();
        var handler = new NutritionInboxQueryHandler(repository,new Clock());
        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new(Actor,Search:new('x',201)),CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new(Actor with { CanManage=false,ViewAll=true }),CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new(Actor with { UserId=Guid.Empty }),CancellationToken.None));
        Assert.Empty(repository.ReceivedCalls());
    }

    [Fact]
    public async Task Page_PropagatesScopeFiltersClockAndToken_CountsClinicalCriticalsWithoutMeals()
    {
        var repository = Substitute.For<INutritionInboxRepository>(); var actor = Actor;
        var p = new NutritionPatient(68.5,161,38,"F",26.4,Hba1c:6.5,NutRiesgo:"alto",SleepRF:2,RiesgoNum:4);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var row = new NutritionInboxRow(Guid.Parse("22222222-2222-2222-2222-222222222222"),"Ana Ruiz","por_renovar",
            Guid.Parse("55555555-5555-5555-5555-555555555555"),2,7,true,1850,
            new(2026,10,12),new(2026,11,10),true,JsonSerializer.Serialize(p,json),JsonSerializer.Serialize(NutritionEngine.ComputeTargets(p),json), "failed", true, "attempts_exhausted", new DateTime(2026,11,6,2,0,0,DateTimeKind.Utc));
        repository.ListAsync(actor,2,10,"por_renovar","Ana",new DateTime(2026,11,6,2,0,0,DateTimeKind.Utc),Arg.Any<CancellationToken>())
            .Returns(new NutritionInboxData([row],11));
        using var cancel = new CancellationTokenSource();
        var result = await new NutritionInboxQueryHandler(repository,new Clock()).Handle(new(actor,2,10,"por_renovar"," Ana "),cancel.Token);
        Assert.Equal(11,result.Total); Assert.Equal(2,result.Page); Assert.Equal(10,result.PageSize);
        var item = Assert.Single(result.Items);
        Assert.Equal("failed",item.GenerationState); Assert.True(item.GenerationFallback);
        Assert.Equal("attempts_exhausted",item.GenerationReason); Assert.Equal(row.GenerationUpdatedAt,item.GenerationUpdatedAt);
        var prescription = item.Prescription!;
        Assert.Equal(row.PrescriptionId,prescription.Id); Assert.Equal(4,prescription.CriticalAlertCount);
        Assert.Equal("por_renovar",prescription.State); Assert.Equal(1850,prescription.K);
        await repository.Received(1).ListAsync(actor,2,10,"por_renovar","Ana",new DateTime(2026,11,6,2,0,0,DateTimeKind.Utc),cancel.Token);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new NutritionInboxQueryHandler(repository,new Clock()).Handle(new(actor),cancel.Token));
        Assert.Single(repository.ReceivedCalls());
    }

    [Fact]
    public async Task Reviewer_EmptySearch_AndNoPrescription_HaveStableEnvelope()
    {
        var actor = Actor with { CanManage=false,CanApprove=true };
        var repository = Substitute.For<INutritionInboxRepository>();
        var row = new NutritionInboxRow(Guid.Parse("11111111-1111-1111-1111-111111111111"),"Ana Ruiz","sin_plan",
            null,null,null,false,null,null,null,false,null,null);
        repository.ListAsync(actor,1,20,null,null,Arg.Any<DateTime>(),Arg.Any<CancellationToken>()).Returns(new NutritionInboxData([row],1));
        var page = await new NutritionInboxQueryHandler(repository,new Clock()).Handle(new(actor,Search:"  "),CancellationToken.None);
        Assert.Null(Assert.Single(page.Items).Prescription);
        var actual = JsonSerializer.Serialize(page,new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal("{\"items\":[{\"patientId\":\"11111111-1111-1111-1111-111111111111\",\"patientName\":\"Ana Ruiz\",\"state\":\"sin_plan\",\"prescription\":null,\"generationState\":null,\"generationFallback\":false,\"generationReason\":null,\"generationUpdatedAt\":null}],\"total\":1,\"page\":1,\"pageSize\":20}",actual);
    }
}
