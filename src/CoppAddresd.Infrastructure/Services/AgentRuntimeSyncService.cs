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

        // La config del backend es un JSON string; el AI Service espera un dict
        // (validación Pydantic). Se parsea a JsonElement para enviarlo como objeto.
        var body = new
        {
            payload.AgentTypeId,
            payload.VersionId,
            payload.VersionNumber,
            payload.Name,
            payload.Description,
            payload.Specialty,
            payload.IconKey,
            payload.Slug,
            Config = JsonSerializer.Deserialize<JsonElement>(payload.Config),
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            settings.Value.SyncAgentConfigEndpoint)
        {
            Content = JsonContent.Create(body, options: JsonOpts),
        };
        request.Headers.Add("X-Internal-Key", internalKey);

        using var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        logger.LogDebug("Configuración de agente sincronizada: {AgentTypeId} v{VersionNumber}",
            payload.AgentTypeId, payload.VersionNumber);
    }

    public async Task<AgentDocumentIngestResult> IngestDocumentAsync(
        AgentDocumentIngestPayload payload,
        CancellationToken ct = default)
    {
        var internalKey = settings.Value.InternalApiKey;
        if (string.IsNullOrWhiteSpace(internalKey))
        {
            logger.LogWarning(
                "AiService:InternalApiKey no configurada; no se puede indexar {DocumentId}",
                payload.DocumentId);
            return new AgentDocumentIngestResult("error", 0, 0,
                "AiService:InternalApiKey no configurada.");
        }

        var body = new
        {
            payload.DocumentId,
            payload.KnowledgeBaseId,
            Filename = payload.FileName,
            Content = payload.ContentBase64,
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            settings.Value.IngestDocumentEndpoint)
        {
            Content = JsonContent.Create(body, options: JsonOpts),
        };
        request.Headers.Add("X-Internal-Key", internalKey);

        try
        {
            using var response = await httpClient.SendAsync(request, ct);

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<IngestResponseDto>(responseBody, JsonOpts);
                return new AgentDocumentIngestResult(
                    result?.Status ?? "indexado",
                    result?.ChunksCreated ?? 0,
                    result?.ReplacedChunks ?? 0,
                    result?.Error);
            }

            logger.LogWarning("Ingestión de {DocumentId} rechazada: {Status} {Body}",
                payload.DocumentId, response.StatusCode, responseBody);
            return new AgentDocumentIngestResult("error", 0, 0,
                $"AI Service respondió {(int)response.StatusCode}: {responseBody}");
        }
        catch (Exception exc) when (exc is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exc, "No se pudo contactar al AI Service para indexar {DocumentId}",
                payload.DocumentId);
            return new AgentDocumentIngestResult("error", 0, 0,
                "AI Service no disponible.");
        }
    }

    public async Task DeleteDocumentChunksAsync(Guid documentId, CancellationToken ct = default)
    {
        var internalKey = settings.Value.InternalApiKey;
        if (string.IsNullOrWhiteSpace(internalKey))
        {
            logger.LogWarning(
                "AiService:InternalApiKey no configurada; no se eliminan chunks de {DocumentId}",
                documentId);
            return;
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"{settings.Value.IngestDocumentEndpoint}/{documentId}");
        request.Headers.Add("X-Internal-Key", internalKey);

        try
        {
            using var response = await httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception exc) when (exc is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exc, "No se pudieron eliminar los chunks de {DocumentId}", documentId);
        }
    }

    private sealed class IngestResponseDto
    {
        public string Status { get; set; } = default!;
        public int ChunksCreated { get; set; }
        public int ReplacedChunks { get; set; }
        public string? Error { get; set; }
    }
}