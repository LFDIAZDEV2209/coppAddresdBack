using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.HealthTests;
using CoppAddresd.Application.Features.HealthTests.Events;
using CoppAddresd.Application.Features.HealthTests.Execution;
using CoppAddresd.Application.Features.HealthTests.Scoring;
using CoppAddresd.Application.Features.ProgramProgress.Assignment;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.HealthTests;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CoppAddresd.UnitTests.ProgramProgress;

public sealed class AssignmentHookTests
{
    private static IOptionsMonitor<ProgramAssignmentSettings> Settings(bool enabled)
    {
        var monitor = Substitute.For<IOptionsMonitor<ProgramAssignmentSettings>>();
        monitor.CurrentValue.Returns(new ProgramAssignmentSettings { Enabled = enabled });
        return monitor;
    }

    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(true, false, 0)]
    [InlineData(true, true, 1)]
    public async Task SubmitReplay_FlagYBateriaCompletaControlanLaPublicacion(bool enabled, bool complete, int events)
    {
        var tests = Substitute.For<IHealthTestRepository>();
        var assignments = Substitute.For<IProgramAssignmentRepository>();
        var publisher = Substitute.For<IPublisher>();
        var assignment = new HealthTestAssignment { Id = Guid.NewGuid(), PatientId = Guid.NewGuid(),
            BatteryAssignmentId = Guid.NewGuid(), Status = HealthTestAssignmentStatus.completed };
        var evaluation = new HealthTestEvaluation { Id = Guid.NewGuid(), AssignmentId = assignment.Id,
            PatientId = assignment.PatientId, Status = HealthTestEvaluationStatus.completed, ClientRequestId = "same-key" };
        tests.GetAssignmentForSubmitAsync(assignment.Id, Arg.Any<CancellationToken>()).Returns(assignment);
        tests.GetAssignmentByIdAsync(assignment.Id, Arg.Any<CancellationToken>()).Returns(assignment);
        tests.GetEvaluationByAssignmentAsync(assignment.Id, Arg.Any<CancellationToken>()).Returns(evaluation.Id);
        tests.GetEvaluationWithDetailsAsync(evaluation.Id, Arg.Any<CancellationToken>()).Returns(evaluation);
        var committed = false;
        tests.ExecuteInTransactionAsync(Arg.Any<Func<Task<HealthTestEvaluationDto>>>(), Arg.Any<CancellationToken>())
            .Returns(async call => { var dto = await call.Arg<Func<Task<HealthTestEvaluationDto>>>()(); committed = true; return dto; });
        assignments.FindCompletedInitialBatteryAsync(assignment.PatientId, assignment.BatteryAssignmentId.Value, Arg.Any<CancellationToken>())
            .Returns(complete ? assignment.BatteryAssignmentId : null);
        publisher.Publish(Arg.Any<BatteryCompletedEvent>(), Arg.Any<CancellationToken>())
            .Returns(_ => { Assert.True(committed); return Task.CompletedTask; });
        using var services = new ServiceCollection().BuildServiceProvider();
        var handler = new SubmitEvaluationCommandHandler(tests, new ScoreStrategyRegistry(services),
            new ScoreRangeEngine(), new IndicatorEngine(), new AlertEngine(), publisher: publisher,
            assignmentRepository: assignments, assignmentSettings: Settings(enabled));
        var dto = await handler.Handle(new(assignment.Id, new([], "same-key")), CancellationToken.None);
        Assert.Equal(evaluation.Id, dto.Id);
        await publisher.Received(events).Publish(Arg.Any<BatteryCompletedEvent>(), Arg.Any<CancellationToken>());
        if (!enabled) await assignments.DidNotReceive().HasCurrentEnrollmentAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await tests.DidNotReceive().AddResultsRangeAsync(Arg.Any<IEnumerable<HealthTestResult>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Evento_FalloOFlagOff_NoRompeElSubmitNiCreaNarrativa()
    {
        var repository = Substitute.For<IProgramAssignmentRepository>();
        repository.ExecuteForPatientAsync(Arg.Any<Guid>(), Arg.Any<Func<Task>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("fallo simulado"));
        var queue = Substitute.For<IProgramNarrativeQueue>();
        var settings = Settings(false);
        var handler = new BatteryCompletedEventHandler(repository, Substitute.For<IProgramRepository>(), new AssignmentEngine(),
            settings, new ConfigurationBuilder().Build(), queue, Substitute.For<ICacheService>(),
            NullLogger<BatteryCompletedEventHandler>.Instance);
        var notification = new BatteryCompletedEvent(Guid.NewGuid(), Guid.NewGuid());
        await handler.Handle(notification, CancellationToken.None);
        await repository.DidNotReceive().ExecuteForPatientAsync(Arg.Any<Guid>(), Arg.Any<Func<Task>>(), Arg.Any<CancellationToken>());
        settings.CurrentValue.Enabled = true;
        await handler.Handle(notification, CancellationToken.None);
        queue.DidNotReceive().TryEnqueue(Arg.Any<Guid>());
    }

    [Fact]
    public async Task Narrativa_FalloDelProveedorConservaPropuestaYEstadoPendiente()
    {
        var repository = Substitute.For<IProgramAssignmentRepository>();
        var client = Substitute.For<IProgramNarrativeClient>();
        var recommendation = new ProgramAssignmentRecommendation { Id = Guid.NewGuid(), Proposal = "{\"matchedRules\":[]}" };
        repository.TryStartNarrativeAsync(recommendation.Id, Arg.Any<CancellationToken>()).Returns(true);
        repository.GetRecommendationAsync(recommendation.Id, Arg.Any<CancellationToken>()).Returns(recommendation);
        client.GenerateAsync(Arg.Any<ProgramNarrativeRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("503"));
        var processor = new ProgramNarrativeProcessor(repository, client, NullLogger<ProgramNarrativeProcessor>.Instance);
        Assert.False(await processor.ProcessAsync(recommendation.Id, CancellationToken.None));
        Assert.Equal("{\"matchedRules\":[]}", recommendation.Proposal);
        await repository.DidNotReceive().SaveNarrativeAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Narrativa_ExitoPersisteAmbosIdiomasSinCambiarDecisiones()
    {
        var repository = Substitute.For<IProgramAssignmentRepository>();
        var client = Substitute.For<IProgramNarrativeClient>();
        var id = Guid.NewGuid();
        repository.TryStartNarrativeAsync(id, Arg.Any<CancellationToken>()).Returns(true);
        repository.GetRecommendationAsync(id, Arg.Any<CancellationToken>()).Returns(new ProgramAssignmentRecommendation());
        client.GenerateAsync(Arg.Any<ProgramNarrativeRequest>(), Arg.Any<CancellationToken>()).Returns(new ProgramNarrativeResponse("ES", "EN", "modelo-prueba"));
        Assert.True(await new ProgramNarrativeProcessor(repository, client, NullLogger<ProgramNarrativeProcessor>.Instance).ProcessAsync(id, CancellationToken.None));
        await repository.Received(1).SaveNarrativeAsync(id, "ES", "EN", "modelo-prueba", Arg.Any<CancellationToken>());
    }
}
