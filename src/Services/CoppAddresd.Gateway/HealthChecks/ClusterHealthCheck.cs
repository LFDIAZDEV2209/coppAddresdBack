using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CoppAddresd.Gateway.HealthChecks;

/// <summary>
/// Sonda activa por cluster: hace <c>GET {baseUrl}/health</c> con un timeout de 2 s
/// (REQ-GW-009). El estado del cluster depende de la respuesta HTTP del backend.
/// </summary>
public sealed class ClusterHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly string _clusterId;
    private readonly Uri _healthUri;

    public ClusterHealthCheck(HttpClient httpClient, string clusterId, string baseUrl, string healthPath)
    {
        _httpClient = httpClient;
        _clusterId = clusterId;
        _healthUri = new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), healthPath.TrimStart('/'));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(_healthUri, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy($"Cluster {_clusterId} responde en {_healthUri}");
            }

            return HealthCheckResult.Unhealthy(
                $"Cluster {_clusterId} respondió {(int)response.StatusCode} en {_healthUri}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // El cliente canceló o el timeout de la sonda expiró.
            return HealthCheckResult.Unhealthy($"Cluster {_clusterId} sin respuesta en {_healthUri}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                $"Cluster {_clusterId} inalcanzable en {_healthUri}", ex);
        }
    }
}
