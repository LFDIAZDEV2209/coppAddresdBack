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

    /// <summary>Endpoint interno de extracción de métricas de exámenes de laboratorio (AI Service).</summary>
    public string LabExamEndpoint { get; set; } = "/chat/lab-exam";

    /// <summary>
    /// Endpoint interno de narración empática de exámenes de laboratorio (AI Service).
    /// Sibling del de extracción: recibe la tabla de evolución pre-computada en .NET.
    /// </summary>
    public string NarrateEndpoint { get; set; } = "/chat/lab-exam/narrate";

    /// <summary>
    /// Timeout (segundos) de la llamada de narración. Best-effort: al vencer, el
    /// handler cae al summary técnico sin romper el upload.
    /// </summary>
    public int LabExamNarrationTimeoutSeconds { get; set; } = 20;
}
