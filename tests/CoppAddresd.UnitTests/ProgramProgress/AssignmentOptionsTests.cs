using CoppAddresd.Api.Extensions;
using CoppAddresd.Application.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CoppAddresd.UnitTests.ProgramProgress;

public sealed class AssignmentOptionsTests
{
    [Theory]
    [InlineData(true, null, true)]
    [InlineData(false, null, false)]
    [InlineData(true, "false", false)]
    [InlineData(false, "true", true)]
    public void Flag_DefaultPorEntornoYOverride(bool development, string? configured, bool expected)
    {
        var values = configured is null ? new Dictionary<string, string?>()
            : new() { ["Program:Assignment:Enabled"] = configured };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        using var services = new ServiceCollection().AddProgramAssignmentSettings(configuration, development).BuildServiceProvider();
        Assert.Equal(expected, services.GetRequiredService<IOptionsMonitor<ProgramAssignmentSettings>>().CurrentValue.Enabled);
    }

    [Fact]
    public void Flag_RecargaPermiteApagarElHook()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        using var services = new ServiceCollection().AddProgramAssignmentSettings(configuration, true).BuildServiceProvider();
        var monitor = services.GetRequiredService<IOptionsMonitor<ProgramAssignmentSettings>>();
        Assert.True(monitor.CurrentValue.Enabled);
        configuration["Program:Assignment:Enabled"] = "false";
        configuration.Reload();
        Assert.False(monitor.CurrentValue.Enabled);
    }
}
