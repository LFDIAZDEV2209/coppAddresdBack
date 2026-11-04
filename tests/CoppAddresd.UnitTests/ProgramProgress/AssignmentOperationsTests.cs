using System.Text.Json;
using CoppAddresd.Api.Controllers;
using CoppAddresd.Api.Context;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.Notifications;
using CoppAddresd.Application.Features.ProgramProgress.Assignment;
using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CoppAddresd.UnitTests.ProgramProgress;

public sealed class AssignmentOperationsTests
{
    private sealed class Clock(DateTimeOffset value) : TimeProvider
    {
        public DateTimeOffset Value=value;
        public override DateTimeOffset GetUtcNow()=>Value;
    }
    private static IOptionsMonitor<ProgramAssignmentSettings> Settings()
    {
        var settings=Substitute.For<IOptionsMonitor<ProgramAssignmentSettings>>();
        settings.CurrentValue.Returns(new ProgramAssignmentSettings { Enabled=true });
        return settings;
    }

    [Theory]
    [InlineData(nameof(ProgramController.ListAssignments))]
    [InlineData(nameof(ProgramController.GetAssignment))]
    [InlineData(nameof(ProgramController.ApplyAssignment))]
    [InlineData(nameof(ProgramController.DismissAssignment))]
    [InlineData(nameof(ProgramController.RegenerateNarrative))]
    public void Endpoints_ExigenProgramEnroll(string method)
    {
        var permissions=typeof(ProgramController).GetMethod(method)!.GetCustomAttributes(typeof(CoppAddresd.Api.Authorization.RequirePermissionAttribute),true)
            .Cast<CoppAddresd.Api.Authorization.RequirePermissionAttribute>();
        Assert.Contains(permissions,p=>p.PermissionCode=="Program.Enroll");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Dismiss_DevuelveFicha_InvalidaCacheSoloSiCambio(bool changed)
    {
        var repository = Substitute.For<IProgramAssignmentOperationsRepository>();
        var cache = Substitute.For<ICacheService>();
        var id = Guid.NewGuid(); var actor = Guid.NewGuid(); var patient = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 11, 4, 15, 0, 0, TimeSpan.Zero);
        var detail = new AssignmentDetailDto(id, patient, "Paciente", null,
            new(Guid.NewGuid(), "PendingAssignment", 0, new DateOnly(2026,11,4), now.UtcDateTime, "America/Bogota"),
            JsonSerializer.SerializeToElement(new{}), JsonSerializer.SerializeToElement(new{}),
            null, null, true, "dismissed", "test-v1", null, null, actor, now.UtcDateTime,
            now.UtcDateTime, null, now.UtcDateTime);
        repository.DismissAsync(id, actor, Arg.Any<IReadOnlyList<Guid>?>(), now.UtcDateTime, Arg.Any<CancellationToken>())
            .Returns((detail, changed));
        var handler = new DismissAssignmentCommandHandler(repository, cache, new Clock(now));
        Assert.Equal(detail, await handler.Handle(new(id, actor, [patient]), CancellationToken.None));
        await repository.Received(1).DismissAsync(id, actor,
            Arg.Is<IReadOnlyList<Guid>?>(ids => ids!.SequenceEqual(new[]{patient})), now.UtcDateTime, Arg.Any<CancellationToken>());
        await cache.Received(changed ? 1 : 0).SetAsync("program-assignment-revision:v1",
            Arg.Any<AssignmentCacheRevision>(), TimeSpan.FromDays(1), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CanalUc001_ReintentoPuedeOmitirPushYChatSinConsultarTokens()
    {
        var devices=Substitute.For<IDeviceTokenRepository>();var fcm=Substitute.For<IFcmClient>();var ai=Substitute.For<IAiServiceClient>();
        var handler=new SendPushNotificationCommandHandler(devices,fcm,ai,NullLogger<SendPushNotificationCommandHandler>.Instance);
        await handler.Handle(new(Guid.NewGuid(),"Título","Mensaje",SkipPush:true,SkipChat:true),CancellationToken.None);
        await devices.DidNotReceive().GetByUserIdAsync(Arg.Any<Guid>(),Arg.Any<CancellationToken>());
        Assert.Empty(ai.ReceivedCalls());Assert.Empty(fcm.ReceivedCalls());
    }

    [Fact]
    public async Task Cola_EndpointConFakes_PropagaSoloScopeServidorYDeniegaScopeVacio()
    {
        var actor=Substitute.For<IProgramActorContext>();
        var patient=Guid.NewGuid();
        actor.UserId.Returns(Guid.NewGuid());
        actor.ResolveScopedPatientIdsAsync(Arg.Any<CancellationToken>()).Returns(new Guid[]{patient});
        var mediator=Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListProgramAssignmentsQuery>(),Arg.Any<CancellationToken>()).Returns(new AssignmentQueuePage([],0,2,10,1));
        var controller=new ProgramController(mediator,NullLogger<ProgramController>.Instance,new ConfigurationBuilder().Build(),
            actor,Substitute.For<IObjectStorageService>());
        var response=await controller.ListAssignments("proposed",2,10,"Ana",CancellationToken.None);
        Assert.IsType<OkObjectResult>(response.Result);
        await mediator.Received(1).Send(Arg.Is<ListProgramAssignmentsQuery>(q=>q.Page==2 && q.Search=="Ana"
            && q.ScopedPatientIds!.SequenceEqual(new[]{patient})),Arg.Any<CancellationToken>());
        actor.ResolveScopedPatientIdsAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Guid>());
        Assert.IsType<ForbidResult>((await controller.ListAssignments()).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.ListAssignments("invalid")).Result);
    }

    [Fact]
    public async Task Cola_Cache30Segundos_SeparaProfesionalesYFiltroAntesDePaginacion()
    {
        var repository=Substitute.For<IProgramAssignmentOperationsRepository>();
        var cache=Substitute.For<ICacheService>();
        var keys=new List<string>();
        cache.GetOrCreateAsync(Arg.Any<string>(),Arg.Any<TimeSpan>(),Arg.Any<Func<CancellationToken,Task<AssignmentQueuePage>>>(),Arg.Any<CancellationToken>())
            .Returns(async call=>
            {
                keys.Add(call.ArgAt<string>(0));
                Assert.Equal(TimeSpan.FromSeconds(30),call.ArgAt<TimeSpan>(1));
                return await call.ArgAt<Func<CancellationToken,Task<AssignmentQueuePage>>>(2)(call.ArgAt<CancellationToken>(3));
            });
        repository.ListAsync(Arg.Any<string?>(),Arg.Any<int>(),Arg.Any<int>(),Arg.Any<string?>(),Arg.Any<IReadOnlyList<Guid>?>(),Arg.Any<DateTime>(),Arg.Any<CancellationToken>())
            .Returns(new AssignmentQueuePage([],0,1,100,1));
        var handler=new ListProgramAssignmentsQueryHandler(repository,cache,new Clock(DateTimeOffset.UtcNow));
        var a=new[]{Guid.NewGuid()};var b=new[]{Guid.NewGuid()};
        await handler.Handle(new(null,-1,1000,null,"professional-a",a),CancellationToken.None);
        await handler.Handle(new(null,-1,1000,null,"professional-b",b),CancellationToken.None);
        Assert.NotEqual(keys[0],keys[1]);
        await repository.Received(1).ListAsync(null,1,100,null,a,Arg.Any<DateTime>(),Arg.Any<CancellationToken>());
        var empty=await handler.Handle(new(null,1,20,null,"no-scope",[]),CancellationToken.None);
        Assert.Empty(empty.Items);
    }

    [Fact]
    public async Task Apply_OverrideYActorDelJwt_UsaElRelojEInvalidaCache()
    {
        var programs=Substitute.For<IProgramRepository>();var cache=Substitute.For<ICacheService>();
        var notifier=Substitute.For<IProgramAssignmentNotifier>();
        var now=new DateTimeOffset(2026,11,4,15,0,0,TimeSpan.Zero);
        var id=Guid.NewGuid();var actor=Guid.NewGuid();var patient=Guid.NewGuid();
        var expected=new AppliedAssignmentDto(id,Guid.NewGuid(),patient,"edited",new DateOnly(2026,11,4),1,JsonSerializer.SerializeToElement(new{routine="Full Body"}),false);
        programs.ActivateAssignmentAsync(Arg.Any<AssignmentActivationInput>(),Arg.Any<CancellationToken>()).Returns(expected);
        var handler=new ApplyAssignmentCommandHandler(programs,cache,notifier,new Clock(now),NullLogger<ApplyAssignmentCommandHandler>.Instance);
        Assert.Equal(expected,await handler.Handle(new(id,OverrideRoutineName:"Full Body",ActorId:actor,ScopedPatientIds:[patient]),CancellationToken.None));
        await programs.Received(1).ActivateAssignmentAsync(Arg.Is<AssignmentActivationInput>(x=>x.ActorId==actor
            && x.OverrideRoutineName=="Full Body" && x.UtcNow==now.UtcDateTime && !x.UseTemplateDefaults),Arg.Any<CancellationToken>());
        await notifier.Received(1).DispatchAsync(id,now.UtcDateTime,Arg.Any<CancellationToken>());
        await cache.Received(1).RemoveAsync(CacheKeys.ScoresHistory(patient),Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Job_RelojFake_Dia6NoActiva_Dia7UnaSolaVez()
    {
        var repo=Substitute.For<IProgramAssignmentOperationsRepository>();var publisher=Substitute.For<IPublisher>();
        var sender=Substitute.For<ISender>();var notifier=Substitute.For<IProgramAssignmentNotifier>();
        var created=new DateTime(2026,10,1,12,0,0,DateTimeKind.Utc);var id=Guid.NewGuid();
        var activated=false;
        repo.ListRecoveryAsync(Arg.Any<DateTime>(),Arg.Any<int>(),Arg.Any<int>(),Arg.Any<CancellationToken>()).Returns(Array.Empty<AssignmentRecoveryCandidate>());
        repo.ListFallbacksAsync(Arg.Any<DateTime>(),Arg.Any<int>(),Arg.Any<CancellationToken>())
            .Returns(_=>activated ? Array.Empty<AssignmentFallbackCandidate>() : new[]{new AssignmentFallbackCandidate(id,created)});
        sender.Send(Arg.Any<ApplyAssignmentCommand>(),Arg.Any<CancellationToken>()).Returns(call=>
        {
            Assert.True(call.Arg<ApplyAssignmentCommand>().UseTemplateDefaults);
            activated=true;
            return new AppliedAssignmentDto(id,Guid.NewGuid(),Guid.NewGuid(),"fallback",new DateOnly(2026,10,8),1,JsonSerializer.SerializeToElement(new{}),false);
        });
        var clock=new Clock(new DateTimeOffset(created.AddDays(6)));
        var job=new ProgramAssignmentJob(repo,publisher,sender,notifier,Settings(),clock,NullLogger<ProgramAssignmentJob>.Instance);
        Assert.Equal(0,(await job.RunAsync(CancellationToken.None)).Fallbacks);
        await sender.DidNotReceive().Send(Arg.Any<ApplyAssignmentCommand>(),Arg.Any<CancellationToken>());
        clock.Value=new DateTimeOffset(created.AddDays(7));
        Assert.Equal(1,(await job.RunAsync(CancellationToken.None)).Fallbacks);
        Assert.Equal(0,(await job.RunAsync(CancellationToken.None)).Fallbacks);
        await sender.Received(1).Send(Arg.Any<ApplyAssignmentCommand>(),Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Avisos_ReintentoOmitePushYaEntregado_YConservaCopyBilingue()
    {
        var repo=Substitute.For<IProgramAssignmentNoticeRepository>();var sender=Substitute.For<ISender>();
        var now=DateTime.UtcNow;var id=Guid.NewGuid();
        repo.ListPendingAsync(null,now,Arg.Any<int>(),Arg.Any<CancellationToken>()).Returns(new[]{id});
        repo.TryClaimAsync(id,now,Arg.Any<CancellationToken>()).Returns(new CoppAddresd.Domain.Entities.ProgramProgress.ProgramAssignmentNotice
        { Id=id,UserId=Guid.NewGuid(),PushCompleted=true,MessageEs="ES",MessageEn="EN",TitleEs="Título",TitleEn="Title",Link="/program" });
        sender.Send(Arg.Any<SendPushNotificationCommand>(),Arg.Any<CancellationToken>()).Returns(new SendPushNotificationResult(0,0,"thread"));
        var settings=Settings();settings.CurrentValue.NotificationLanguage="en";
        var notifier=new ProgramAssignmentNotifier(repo,sender,settings,NullLogger<ProgramAssignmentNotifier>.Instance);
        Assert.Equal(1,await notifier.DispatchAsync(null,now,CancellationToken.None));
        await sender.Received(1).Send(Arg.Is<SendPushNotificationCommand>(c=>c.SkipPush && !c.SkipChat && c.Body=="EN"
            && c.Data!["body_es"]=="ES" && c.Data["event_type"]=="program-ready"),Arg.Any<CancellationToken>());
    }
}
