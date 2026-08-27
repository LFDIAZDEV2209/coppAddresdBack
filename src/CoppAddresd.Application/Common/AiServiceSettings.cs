namespace CoppAddresd.Application.Common;

public class AiServiceSettings
{
    public const string SectionName = "AiService";
    
    public string BaseUrl { get; set; } = "http://localhost:8000";
    public string ApiPrefix { get; set; } = "/api/v1";
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>Clave compartida entre backend y AI Service para endpoints internos.</summary>
    public string InternalApiKey { get; set; } = string.Empty;

    public string ChatEndpoint => $"{ApiPrefix}/chat";
    public string StreamEndpoint => $"{ApiPrefix}/chat/stream";
    public string ExecutionsEndpoint => $"{ApiPrefix}/admin/executions";
    public string SyncAgentConfigEndpoint => "/internal/agents/sync-config";
    public string IngestDocumentEndpoint => "/internal/agents/ingest";

    /// <summary>Endpoint interno de generación de planes de alimentación/rutinas (AI Service).</summary>
    public string PlanGenerateEndpoint { get; set; } = "/internal/wellness/generate-plan";

    /// <summary>
    /// Endpoint interno de inyección de mensajes proactivos del bot (sin LLM,
    /// costo cero). Se omite <c>thread_id</c> en el payload para que el AI
    /// Service use el thread estable <c>proactive-{userId}</c>.
    /// </summary>
    public string ProactiveMessageEndpoint { get; set; } = "/internal/agents/proactive-message";
}
