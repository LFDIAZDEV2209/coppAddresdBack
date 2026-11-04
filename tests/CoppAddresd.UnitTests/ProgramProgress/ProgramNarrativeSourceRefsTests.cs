using System.Text.Json;
using CoppAddresd.Application.Features.ProgramProgress.Assignment;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.ProgramProgress;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.UnitTests.ProgramProgress;

public sealed class ProgramNarrativeSourceRefsTests
{
    private static async Task<ProgramNarrativeRequest> ProcessAsync(string snapshot)
    {
        var id = Guid.NewGuid();
        var repository = Substitute.For<IProgramAssignmentRepository>();
        repository.TryStartNarrativeAsync(id, Arg.Any<CancellationToken>()).Returns(true);
        repository.GetRecommendationAsync(id, Arg.Any<CancellationToken>()).Returns(new ProgramAssignmentRecommendation
            { Id = id, State = "applied", ProfileSnapshot = snapshot, Proposal = "{\"matchedRules\":[]}" });
        var client = Substitute.For<IProgramNarrativeClient>();
        ProgramNarrativeRequest? payload = null;
        client.GenerateAsync(Arg.Any<ProgramNarrativeRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            payload = call.Arg<ProgramNarrativeRequest>();
            return new ProgramNarrativeResponse("ES", "EN");
        });
        Assert.True(await new ProgramNarrativeProcessor(repository, client, NullLogger<ProgramNarrativeProcessor>.Instance)
            .ProcessAsync(id, CancellationToken.None));
        await repository.Received(1).SaveNarrativeAsync(id, "ES", "EN", null, Arg.Any<CancellationToken>());
        return Assert.IsType<ProgramNarrativeRequest>(payload);
    }

    [Fact]
    public async Task Processor_FuentesSoloDeCondicionesActivas_SinVaciosNiDuplicados()
    {
        const string snapshot = """
            {"conditions":[
              {"active":true,"sourceRef":" ADA 2026 "},
              {"active":true,"sourceRef":"ADA 2026"},
              {"active":false,"sourceRef":"No enviar"},
              {"sourceRef":"Sin active"},
              {"active":true,"sourceRef":"   "},
              {"active":true,"sourceRef":null},
              {"active":true,"sourceRef":42},
              {"active":true,"sourceRef":"OMS"},null],
             "proposalSource":"No derivar de otros campos"}
            """;
        var request = await ProcessAsync(snapshot);
        Assert.Equal(new[] { "ADA 2026", "OMS" }, request.SourceRefs);
        Assert.Equal(snapshot, request.ProfileSnapshot.GetRawText());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"conditions\":[]}")]
    [InlineData("{\"conditions\":null}")]
    [InlineData("{\"conditions\":[{\"active\":false,\"sourceRef\":\"OMS\"}]}")]
    public async Task Processor_SinFuentesActivas_EnviaArrayVacio(string snapshot)
    {
        var request = await ProcessAsync(snapshot);
        Assert.Empty(request.SourceRefs);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(request));
        Assert.Equal(JsonValueKind.Array, json.RootElement.GetProperty("source_refs").ValueKind);
        Assert.Equal(0, json.RootElement.GetProperty("source_refs").GetArrayLength());
    }

    [Fact]
    public async Task Processor_LimitaA100FuentesValidas_DeHasta500Caracteres()
    {
        var conditions = new[] { new { active = true, sourceRef = new string('x', 501) },
                new { active = true, sourceRef = new string('y', 500) } }
            .Concat(Enumerable.Range(1, 150).Select(i => new { active = true, sourceRef = "Fuente " + i }));
        var request = await ProcessAsync(JsonSerializer.Serialize(new { conditions }));
        Assert.Equal(100, request.SourceRefs.Count);
        Assert.Equal(new string('y', 500), request.SourceRefs[0]);
        Assert.All(request.SourceRefs, source => Assert.InRange(source.Length, 1, 500));
        Assert.Equal(100, request.SourceRefs.Distinct(StringComparer.Ordinal).Count());
    }
}
