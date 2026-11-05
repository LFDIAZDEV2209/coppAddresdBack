using System.Text.Json;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.ProgramProgress.Assignment;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Services;
using MediatR;
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
        repo.GetNarrativeStateAsync(id, Arg.Any<IReadOnlyList<Guid>?>(), Arg.Any<CancellationToken>()).Returns(state);
        queue.TryEnqueue(id, true).Returns(true);
        var handler = new RegenerateNarrativeCommandHandler(repo, queue, NullLogger<RegenerateNarrativeCommandHandler>.Instance);
        Assert.True((await handler.Handle(new(id, [patient]), CancellationToken.None))!.Queued);
        Assert.True((await handler.Handle(new(id, [patient]), CancellationToken.None))!.Queued);
        await repo.Received(2).GetNarrativeStateAsync(id, Arg.Is<IReadOnlyList<Guid>?>(p => p!.SequenceEqual(new[] { patient })), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Regenerar_InexistenteOFueraDeScope_NoEncola_YDismissedEsConflicto()
    {
        var id = Guid.NewGuid(); var repo = Substitute.For<IProgramAssignmentOperationsRepository>();
        var queue = Substitute.For<IProgramNarrativeQueue>();
        var handler = new RegenerateNarrativeCommandHandler(repo, queue, NullLogger<RegenerateNarrativeCommandHandler>.Instance);
        repo.GetNarrativeStateAsync(id, Arg.Any<IReadOnlyList<Guid>?>(), Arg.Any<CancellationToken>()).Returns((string?)null);
        Assert.Null(await handler.Handle(new(id, []), CancellationToken.None));
        repo.GetNarrativeStateAsync(id, Arg.Any<IReadOnlyList<Guid>?>(), Arg.Any<CancellationToken>()).Returns("dismissed");
        var error = await Assert.ThrowsAsync<BusinessRuleViolationException>(() => handler.Handle(new(id, null), CancellationToken.None));
        Assert.Contains("desestimada", error.Message);
        queue.DidNotReceive().TryEnqueue(Arg.Any<Guid>(), Arg.Any<bool>());
    }

}
