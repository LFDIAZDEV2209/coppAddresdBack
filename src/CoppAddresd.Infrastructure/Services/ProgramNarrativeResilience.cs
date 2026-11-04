using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>Políticas compartidas por el cliente: el circuito conserva sus fallos entre requests y scopes.</summary>
public sealed class ProgramNarrativeResilience
{
    public IAsyncPolicy<HttpResponseMessage> Policy { get; }

    public ProgramNarrativeResilience(ILogger<ProgramNarrativeResilience> logger)
    {
        var retry = HttpPolicyExtensions.HandleTransientHttpError().WaitAndRetryAsync(3,
            attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
            (outcome, delay, attempt, _) => logger.LogWarning(
                "Program.NarrativeHttpRetry Attempt={Attempt} DelaySeconds={DelaySeconds} Status={Status}",
                attempt, delay.TotalSeconds, outcome.Result?.StatusCode));
        var breaker = HttpPolicyExtensions.HandleTransientHttpError().CircuitBreakerAsync(5, TimeSpan.FromSeconds(30),
            onBreak: (outcome, delay) => logger.LogWarning(
                "Program.NarrativeCircuitOpen Seconds={Seconds} Status={Status}", delay.TotalSeconds, outcome.Result?.StatusCode),
            onReset: () => logger.LogInformation("Program.NarrativeCircuitReset"));
        Policy = Polly.Policy.WrapAsync(retry, breaker);
    }
}
