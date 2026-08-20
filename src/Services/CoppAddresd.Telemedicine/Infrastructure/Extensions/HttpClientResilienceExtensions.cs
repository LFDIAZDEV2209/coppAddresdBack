using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

namespace CoppAddresd.Telemedicine.Infrastructure.Extensions;

/// <summary>
/// Resiliencia estándar del proyecto para clientes HTTP (mismo patrón que el
/// backend): reintentos con backoff exponencial + circuit breaker. Aplica al
/// cliente del backend (datos de referencia) y a futuros clientes externos.
/// </summary>
public static class HttpClientResilienceExtensions
{
    public static IHttpClientBuilder AddResiliencePolicy(this IHttpClientBuilder builder)
    {
        return builder.AddPolicyHandler((services, _) =>
        {
            var logger = services.GetRequiredService<ILogger<TelemedicineResilience>>();

            var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                    onRetry: (outcome, delay, attempt, _) =>
                    {
                        logger.LogWarning(
                            "Reintento {Attempt} tras {Delay}s por: {StatusCode}",
                            attempt,
                            delay.TotalSeconds,
                            outcome.Result?.StatusCode);
                    });

            var circuitBreakerPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (outcome, delay) =>
                    {
                        logger.LogWarning(
                            "Circuito abierto por {Delay}s: {StatusCode}",
                            delay.TotalSeconds,
                            outcome.Result?.StatusCode);
                    },
                    onReset: () => logger.LogInformation("Circuito restablecido"),
                    onHalfOpen: () => logger.LogInformation("Circuito semiabierto"));

            return Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
        });
    }
}

internal class TelemedicineResilience;
