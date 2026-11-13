using System.Text.Json;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.Erp;
using CoppAddresd.Application.Features.ProgramProgress.Queries.Erp;
using CoppAddresd.Application.Interfaces;
using NSubstitute;

namespace CoppAddresd.UnitTests.ProgramProgress.Api;

public sealed class PatientOverviewSummaryTests
{
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    { public override DateTimeOffset GetUtcNow() => now; }
    private static PatientOverviewDto Overview(string timezone = "America/Bogota") => new("Paciente QA",
        new(Guid.NewGuid(), Guid.NewGuid(), "Programa de 83 días", "Active", timezone, DateTime.UtcNow, 1, 12),
        null, null, [], [], [], null, [], [], 0, [], [], null);

    [Fact]
    public async Task Handler_AddsProgramAndCycle_UsesLocalDate_AndCountsVersionsOnce()
    {
        var patient = Guid.NewGuid(); var cycle = Guid.NewGuid();
        var programs = Substitute.For<IProgramRepository>(); var nutrition = Substitute.For<INutritionPrescriptionRepository>();
        programs.GetPatientOverviewAsync(patient, Arg.Any<CancellationToken>()).Returns(Overview());
        var start = new DateOnly(2026, 11, 5);
        nutrition.ListCycleHeadersAsync(patient, Arg.Any<CancellationToken>()).Returns(new[]
        {
            new NutritionCycleHeader(Guid.NewGuid(), Guid.NewGuid(), start.AddDays(-30), start.AddDays(-1), DateTime.UtcNow.AddDays(-30), true),
            new(Guid.NewGuid(), cycle, start, start.AddDays(29), DateTime.UtcNow.AddDays(-2), false),
            new(Guid.NewGuid(), cycle, start, start.AddDays(29), DateTime.UtcNow.AddDays(-1), true),
            new(Guid.NewGuid(), Guid.NewGuid(), start.AddDays(30), start.AddDays(59), DateTime.UtcNow, true),
        });
        using var cancellation = new CancellationTokenSource();
        var result = await new GetPatientOverviewQueryHandler(programs, nutrition,
            new Clock(new(2026, 11, 14, 2, 0, 0, TimeSpan.Zero))).Handle(new(patient), cancellation.Token);
        Assert.Equal("Programa de 83 días", result!.ProgramName);
        Assert.Equal(new PatientOverviewCurrentCycle(2, 9, 30, start, start.AddDays(29)), result.CurrentCycle);
        var json = JsonSerializer.SerializeToElement(result);
        Assert.Equal("Programa de 83 días", json.GetProperty("programName").GetString());
        Assert.Equal(30, json.GetProperty("currentCycle").GetProperty("of").GetInt32());
        await nutrition.Received(1).ListCycleHeadersAsync(patient, cancellation.Token);
    }

    [Theory]
    [InlineData(-30)] [InlineData(1)]
    public async Task Handler_ExpiredOrFutureCycle_ReturnsOptionalNull(int offset)
    {
        var patient = Guid.NewGuid(); var programs = Substitute.For<IProgramRepository>();
        var nutrition = Substitute.For<INutritionPrescriptionRepository>();
        programs.GetPatientOverviewAsync(patient, Arg.Any<CancellationToken>()).Returns(Overview("UTC"));
        var today = new DateOnly(2026, 11, 13); var start = today.AddDays(offset);
        nutrition.ListCycleHeadersAsync(patient, Arg.Any<CancellationToken>()).Returns(new[]
        { new NutritionCycleHeader(Guid.NewGuid(), Guid.NewGuid(), start, start.AddDays(29), DateTime.UtcNow, true) });
        var result = await new GetPatientOverviewQueryHandler(programs, nutrition, new Clock(new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero)))
            .Handle(new(patient), CancellationToken.None);
        Assert.Null(result!.CurrentCycle);
    }

    [Fact]
    public async Task Handler_NoEnrollment_PreservesNullAndDoesNotQueryNutrition()
    {
        var programs = Substitute.For<IProgramRepository>(); var nutrition = Substitute.For<INutritionPrescriptionRepository>();
        var result = await new GetPatientOverviewQueryHandler(programs, nutrition, TimeProvider.System)
            .Handle(new(Guid.NewGuid()), CancellationToken.None);
        Assert.Null(result); Assert.Empty(nutrition.ReceivedCalls());
    }
}
