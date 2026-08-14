using System.Net.Http.Json;
using System.Text.Json;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Notifica al AI Service los cambios de configuración de agentes (activación
/// de versiones) mediante el endpoint interno protegido por X-Internal-Key.
/// </summary>
public sealed class AgentRuntimeSyncService(
    HttpClient httpClient,
    IOptions<AiServiceSettings> settings,
    ILogger<AgentRuntimeSyncService> logger) : IAgentRuntimeSyncService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task SyncAgentConfigAsync(AgentRuntimeConfigPayload payload, CancellationToken ct = default)
    {
        var internalKey = settings.Value.InternalApiKey;
        if (string.IsNullOrWhiteSpace(internalKey))
        {
            logger.LogWarning(
                "AiService:InternalApiKey no configurada; se omite la sincronización de {AgentTypeId}",
                payload.AgentTypeId);
            return;
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            settings.Value.SyncAgentConfigEndpoint)
        {
            Content = JsonContent.Create(payload, options: JsonOpts),
        };
        request.Headers.Add("X-Internal-Key", internalKey);

        using var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        logger.LogDebug("Configuración de agente sincronizada: {AgentTypeId} v{VersionNumber}",
            payload.AgentTypeId, payload.VersionNumber);
    }
}