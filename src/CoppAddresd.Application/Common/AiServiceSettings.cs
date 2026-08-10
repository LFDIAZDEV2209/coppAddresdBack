namespace CoppAddresd.Application.Common;

public class AiServiceSettings
{
    public const string SectionName = "AiService";
    
    public string BaseUrl { get; set; } = "http://localhost:8000";
    public string ApiPrefix { get; set; } = "/api/v1";
    public int TimeoutSeconds { get; set; } = 120;
    
    public string ChatEndpoint => $"{ApiPrefix}/chat";
    public string StreamEndpoint => $"{ApiPrefix}/chat/stream";
}
