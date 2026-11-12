namespace CoppAddresd.WhatsApp.Binding;

public sealed class WhatsAppAuthOptions
{
    public string BaseUrl { get; set; } = "http://localhost:5123";

    public Uri GetBaseUri()
    {
        if (!Uri.TryCreate(BaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException("WhatsApp:Auth:BaseUrl debe ser una URL HTTP(S) sin credenciales ni query.");
        return uri;
    }
}
