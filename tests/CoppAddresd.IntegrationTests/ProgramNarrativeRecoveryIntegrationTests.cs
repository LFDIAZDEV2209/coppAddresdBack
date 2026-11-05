using CoppAddresd.Application.Features.ProgramProgress.Assignment;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using MediatR;

namespace CoppAddresd.IntegrationTests;

public sealed partial class ProgramAssignmentIntegrationTests
{
    [AssignmentPostgresFact]
    public async Task Narrativa_RecuperaAplicadasEditadasFallback_YRegeneraConAntiIdorSinReabrirEstados()
    {
        var data = await ScenarioAsync(1);
        await using var db = NewDb();
        var repo = new ProgramAssignmentRepository(db);
        var client = Substitute.For<IProgramNarrativeClient>();
        client.GenerateAsync(Arg.Any<ProgramNarrativeRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ProgramNarrativeResponse("Narrativa ES", "Narrative EN", "modelo-prueba"));
        var processor = new ProgramNarrativeProcessor(repo, client, NullLogger<ProgramNarrativeProcessor>.Instance);
        foreach (var state in new[] { "proposed", "pending-narrative", "applied", "edited", "fallback", "dismissed" })
        {
            await db.ProgramAssignmentRecommendations.Where(r => r.Id == data.RecommendationId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.State, state)
                    .SetProperty(r => r.NarrativeEs, (string?)null).SetProperty(r => r.NarrativeEn, (string?)null)
                    .SetProperty(r => r.UpdatedAt, (DateTime?)null));
            var selected = await repo.ListMissingNarrativesAsync(_lifecycleClock.Current.UtcDateTime.AddMinutes(-1), 10, CancellationToken.None);
            Assert.Equal(state != "dismissed", selected.Contains(data.RecommendationId));
            Assert.True(await processor.ProcessAsync(data.RecommendationId, CancellationToken.None));
            var row = await repo.GetRecommendationAsync(data.RecommendationId, CancellationToken.None);
            Assert.Equal(state is "pending-narrative" or "proposed" ? "proposed" : state, row!.State);
            Assert.Equal(state == "dismissed" ? null : "Narrativa ES", row.NarrativeEs);
            Assert.Equal(state == "dismissed" ? null : "Narrative EN", row.NarrativeEn);
        }

        // La regeneración interna conserva el alcance antes de comprobar el estado.
        using (var scope = _services.CreateScope())
            Assert.Null(await scope.ServiceProvider.GetRequiredService<IMediator>()
                .Send(new RegenerateNarrativeCommand(data.RecommendationId, []), CancellationToken.None));
        using (var scope = _services.CreateScope())
            await Assert.ThrowsAsync<BusinessRuleViolationException>(() => scope.ServiceProvider.GetRequiredService<IMediator>()
                .Send(new RegenerateNarrativeCommand(data.RecommendationId, [data.PatientId]), CancellationToken.None));
        await db.ProgramAssignmentRecommendations.Where(r => r.Id == data.RecommendationId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.State, "applied").SetProperty(r => r.NarrativeEs, "Anterior ES")
                .SetProperty(r => r.NarrativeEn, "Anterior EN"));
        using (var scope = _services.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<IMediator>();
            Assert.True((await sender.Send(new RegenerateNarrativeCommand(data.RecommendationId, [data.PatientId]), CancellationToken.None))!.Queued);
            Assert.True((await sender.Send(new RegenerateNarrativeCommand(data.RecommendationId, [data.PatientId]), CancellationToken.None))!.Queued);
        }
        Assert.Equal("Anterior ES", (await repo.GetRecommendationAsync(data.RecommendationId, CancellationToken.None))!.NarrativeEs);
        var queue = _services.GetRequiredService<IProgramNarrativeQueue>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var reader = queue.ReadAllAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
        Assert.True(await reader.MoveNextAsync());
        Assert.True(reader.Current.Regenerate);
        Assert.True(await processor.ProcessAsync(reader.Current.RecommendationId, timeout.Token, reader.Current.Regenerate));
        queue.Complete(data.RecommendationId);
        var saved = await repo.GetRecommendationAsync(data.RecommendationId, CancellationToken.None);
        Assert.Equal("applied", saved!.State);
        Assert.Equal("Narrativa ES", saved.NarrativeEs);
        Assert.Equal("Narrative EN", saved.NarrativeEn);
        Assert.Equal(data.RecommendationId, saved.Id);
    }
}
