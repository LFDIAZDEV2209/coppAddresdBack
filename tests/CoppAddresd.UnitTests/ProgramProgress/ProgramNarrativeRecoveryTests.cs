using System.Text.Json;
using CoppAddresd.Api.Context;
using CoppAddresd.Api.Controllers;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.ProgramProgress.Assignment;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CoppAddresd.UnitTests.ProgramProgress;

public sealed class ProgramNarrativeRecoveryTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 18, 0, 0, DateTimeKind.Utc);
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(Now); }

    [Theory]
    [InlineData("proposed", null, null, true)]
    [InlineData("pending-narrative", null, null, true)]
    [InlineData("applied", null, null, true)]
    [InlineData("edited", null, null, true)]
    [InlineData("fallback", null, null, true)]
    [InlineData("dismissed", null, null, false)]
    [InlineData("applied", "ES", "EN", false)]
    [InlineData("edited", "ES", null, false)]
    [InlineData("fallback", null, "EN", false)]
    [InlineData("proposed", "ES", "EN", false)]
    public void Select_ConFuenteFake_FiltraEstadosNarrativasDebounceYLote(string state, string? es, string? en, bool included)
    {
        var candidate = new ProgramAssignmentRecommendation { Id = Guid.NewGuid(), State = state,
            NarrativeEs = es, NarrativeEn = en, CreatedAt = Now.AddMinutes(-2) };
        var newer = new ProgramAssignmentRecommendation { Id = Guid.NewGuid(), CreatedAt = Now };
        var retry = new ProgramAssignmentRecommendation { Id = Guid.NewGuid(), CreatedAt = Now.AddHours(-1), UpdatedAt = Now };
        var source = new[] { newer, retry, candidate }.AsQueryable();
        var result = ProgramNarrativeEligibility.SelectPending(source, Now.AddMinutes(-1), 10).ToArray();
        Assert.Equal(included ? new[] { candidate.Id } : [], result);
        var oldest = new ProgramAssignmentRecommendation { Id = Guid.NewGuid(), CreatedAt = Now.AddDays(-1) };
        Assert.Equal(new[] { oldest.Id }, ProgramNarrativeEligibility.SelectPending(source.Append(oldest), Now.AddMinutes(-1), 1));
    }

    [Fact]
    public async Task Recuperacion_PropagaCutoffYLote_RespetaFlagYColaLlena()
    {
        var repo = Substitute.For<IProgramAssignmentRepository>();
        var queue = Substitute.For<IProgramNarrativeQueue>();
        var settings = Substitute.For<IOptionsMonitor<ProgramAssignmentSettings>>();
        settings.CurrentValue.Returns(new ProgramAssignmentSettings { Enabled = true, JobBatchSize = 2, NarrativeDebounceSeconds = 90 });
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        repo.ListMissingNarrativesAsync(Now.AddSeconds(-90), 2, Arg.Any<CancellationToken>()).Returns(ids);
        queue.TryEnqueue(ids[0]).Returns(true);
        var recovery = new ProgramNarrativeRecovery(repo, queue, settings, new Clock());
        Assert.Equal(1, await recovery.RunAsync(CancellationToken.None));
        await repo.Received(1).ListMissingNarrativesAsync(Now.AddSeconds(-90), 2, Arg.Any<CancellationToken>());
        settings.CurrentValue.Enabled = false;
        Assert.Equal(0, await recovery.RunAsync(CancellationToken.None));
        await repo.Received(1).ListMissingNarrativesAsync(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cola_DeduplicaInclusoDuranteProcesado_YPermiteRepetirAlTerminar()
    {
        var queue = new ProgramNarrativeQueue(Options.Create(new ProgramAssignmentSettings { NarrativeQueueCapacity = 1 }));
        var id = Guid.NewGuid();
        Assert.True(queue.TryEnqueue(id, true));
        Assert.True(queue.TryEnqueue(id, true));
        Assert.False(queue.TryEnqueue(Guid.NewGuid()));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var reader = queue.ReadAllAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
        Assert.True(await reader.MoveNextAsync());
        Assert.Equal(new ProgramNarrativeWork(id, true), reader.Current);
        Assert.True(queue.TryEnqueue(id, true));
        queue.Complete(id);
        Assert.True(queue.TryEnqueue(id, true));
        Assert.True(await reader.MoveNextAsync());
        Assert.Equal(id, reader.Current.RecommendationId);
        queue.Complete(id);
    }

    private static AssignmentDetailDto Detail(Guid id, string state) => new(id, Guid.NewGuid(), "Paciente", null,
        new(Guid.NewGuid(), "Active", 1, DateOnly.FromDateTime(Now), Now, "America/Bogota"),
        JsonSerializer.SerializeToElement(new { }), JsonSerializer.SerializeToElement(new { }),
        "Anterior ES", "Anterior EN", false, state, "v1", null, null, null, Now, null, Now, null);

    [Theory]
    [InlineData("applied")]
    [InlineData("edited")]
    [InlineData("fallback")]
    [InlineData("proposed")]
    public async Task Regenerar_ConNarrativaExistente_EncolaSinBorrarYReplayAceptado(string state)
    {
        var id = Guid.NewGuid(); var patient = Guid.NewGuid();
        var repo = Substitute.For<IProgramAssignmentOperationsRepository>();
        var queue = Substitute.For<IProgramNarrativeQueue>();
        var detail = Detail(id, state);
        repo.GetDetailAsync(id, Arg.Any<IReadOnlyList<Guid>?>(), Arg.Any<CancellationToken>()).Returns(detail);
        queue.TryEnqueue(id, true).Returns(true);
        var handler = new RegenerateNarrativeCommandHandler(repo, queue, NullLogger<RegenerateNarrativeCommandHandler>.Instance);
        Assert.True((await handler.Handle(new(id, [patient]), CancellationToken.None))!.Queued);
        Assert.True((await handler.Handle(new(id, [patient]), CancellationToken.None))!.Queued);
        Assert.Equal("Anterior ES", detail.NarrativeEs);
        await repo.Received(2).GetDetailAsync(id, Arg.Is<IReadOnlyList<Guid>?>(p => p!.SequenceEqual(new[] { patient })), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Regenerar_InexistenteOFueraDeScope_NoEncola_YDismissedEsConflicto()
    {
        var id = Guid.NewGuid(); var repo = Substitute.For<IProgramAssignmentOperationsRepository>();
        var queue = Substitute.For<IProgramNarrativeQueue>();
        var handler = new RegenerateNarrativeCommandHandler(repo, queue, NullLogger<RegenerateNarrativeCommandHandler>.Instance);
        Assert.Null(await handler.Handle(new(id, []), CancellationToken.None));
        repo.GetDetailAsync(id, Arg.Any<IReadOnlyList<Guid>?>(), Arg.Any<CancellationToken>()).Returns(Detail(id, "dismissed"));
        var error = await Assert.ThrowsAsync<BusinessRuleViolationException>(() => handler.Handle(new(id, null), CancellationToken.None));
        Assert.Contains("desestimada", error.Message);
        queue.DidNotReceive().TryEnqueue(Arg.Any<Guid>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Regenerar_Endpoint_202Y404_UsaScopeServidor()
    {
        var actor = Substitute.For<IProgramActorContext>(); actor.UserId.Returns(Guid.NewGuid());
        var patient = Guid.NewGuid(); var id = Guid.NewGuid();
        actor.ResolveScopedPatientIdsAsync(Arg.Any<CancellationToken>()).Returns(new[] { patient });
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<RegenerateNarrativeCommand>(), Arg.Any<CancellationToken>()).Returns(new NarrativeQueuedDto(true));
        var controller = new ProgramController(mediator, NullLogger<ProgramController>.Instance,
            new ConfigurationBuilder().Build(), actor, Substitute.For<IObjectStorageService>());
        var accepted = Assert.IsType<AcceptedResult>((await controller.RegenerateNarrative(id, CancellationToken.None)).Result);
        Assert.True(Assert.IsType<NarrativeQueuedDto>(accepted.Value).Queued);
        await mediator.Received(1).Send(Arg.Is<RegenerateNarrativeCommand>(c => c.RecommendationId == id
            && c.ScopedPatientIds!.SequenceEqual(new[] { patient })), Arg.Any<CancellationToken>());
        mediator.Send(Arg.Any<RegenerateNarrativeCommand>(), Arg.Any<CancellationToken>()).Returns((NarrativeQueuedDto?)null);
        Assert.IsType<NotFoundResult>((await controller.RegenerateNarrative(id, CancellationToken.None)).Result);
        actor.UserId.Returns((Guid?)null);
        Assert.IsType<UnauthorizedResult>((await controller.RegenerateNarrative(id, CancellationToken.None)).Result);
    }
}
