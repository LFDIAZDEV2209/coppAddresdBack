using System.Text.Json;
using CoppAddresd.Api.Controllers;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.Notifications;
using CoppAddresd.Application.Features.ProgramProgress.Assignment;
using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CoppAddresd.UnitTests.ProgramProgress;

public sealed class AssignmentOperationsTests
{
    [Fact]
    public void RutasErpDeAsignacion_Retiradas_InsumosConservados()
    {
        var routes = typeof(ProgramController).GetMethods()
            .SelectMany(m => m.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute), true)
                .Cast<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>()).Select(a => a.Template ?? "").ToArray();
        Assert.DoesNotContain(routes, route => route.StartsWith("assignments", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("me/snapshot", routes);
        Assert.Contains("enrollments/{id:guid}/snapshot", routes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FallbackInterno_UsaReloj_InvalidaHistorial_YNoDuplicaAvisos(bool replay)
    {
        var programs = Substitute.For<IProgramRepository>(); var cache = Substitute.For<ICacheService>();
        var notifier = Substitute.For<IProgramAssignmentNotifier>(); var id = Guid.NewGuid(); var patient = Guid.NewGuid();
        var now = new DateTimeOffset(2026,11,4,15,0,0,TimeSpan.Zero);
        var result = new AppliedAssignmentDto(id,Guid.NewGuid(),patient,"fallback",new(2026,11,4),1,
            JsonSerializer.SerializeToElement(new {}),replay);
        programs.ActivateAssignmentFallbackAsync(new(id,now.UtcDateTime),Arg.Any<CancellationToken>()).Returns(result);
        using var source = new CancellationTokenSource();
        var handler = new ApplyAssignmentFallbackCommandHandler(programs,cache,notifier,new Clock(now),
            NullLogger<ApplyAssignmentFallbackCommandHandler>.Instance);
        Assert.Equal(result,await handler.Handle(new(id),source.Token));
        await programs.Received(1).ActivateAssignmentFallbackAsync(new(id,now.UtcDateTime),source.Token);
        await cache.Received(replay ? 0 : 1).RemoveAsync(CacheKeys.ScoresHistory(patient),source.Token);
        await notifier.Received(replay ? 0 : 1).DispatchAsync(id,now.UtcDateTime,source.Token);
    }

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
    public async Task Job_RelojFake_Dia6NoActiva_Dia7UnaSolaVez()
    {
        var repo=Substitute.For<IProgramAssignmentOperationsRepository>();var publisher=Substitute.For<IPublisher>();
        var sender=Substitute.For<ISender>();var notifier=Substitute.For<IProgramAssignmentNotifier>();
        var created=new DateTime(2026,10,1,12,0,0,DateTimeKind.Utc);var id=Guid.NewGuid();
        var activated=false;
        repo.ListRecoveryAsync(Arg.Any<DateTime>(),Arg.Any<int>(),Arg.Any<int>(),Arg.Any<CancellationToken>()).Returns(Array.Empty<AssignmentRecoveryCandidate>());
        repo.ListFallbacksAsync(Arg.Any<DateTime>(),Arg.Any<int>(),Arg.Any<CancellationToken>())
            .Returns(_=>activated ? Array.Empty<AssignmentFallbackCandidate>() : new[]{new AssignmentFallbackCandidate(id,created)});
        sender.Send(Arg.Any<ApplyAssignmentFallbackCommand>(),Arg.Any<CancellationToken>()).Returns(call=>
        {
            activated=true;
            return new AppliedAssignmentDto(id,Guid.NewGuid(),Guid.NewGuid(),"fallback",new DateOnly(2026,10,8),1,JsonSerializer.SerializeToElement(new{}),false);
        });
        var clock=new Clock(new DateTimeOffset(created.AddDays(6)));
        var job=new ProgramAssignmentJob(repo,publisher,sender,notifier,Settings(),clock,NullLogger<ProgramAssignmentJob>.Instance);
        Assert.Equal(0,(await job.RunAsync(CancellationToken.None)).Fallbacks);
        await sender.DidNotReceive().Send(Arg.Any<ApplyAssignmentFallbackCommand>(),Arg.Any<CancellationToken>());
        clock.Value=new DateTimeOffset(created.AddDays(7));
        Assert.Equal(1,(await job.RunAsync(CancellationToken.None)).Fallbacks);
        Assert.Equal(0,(await job.RunAsync(CancellationToken.None)).Fallbacks);
        await sender.Received(1).Send(Arg.Any<ApplyAssignmentFallbackCommand>(),Arg.Any<CancellationToken>());
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
