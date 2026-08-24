namespace CoppAddresd.Gateway.Configuration;

/// <summary>
/// Configuración de CORS del gateway (bind a la sección <c>Cors</c>).
/// La lista de orígenes proviene de <c>Cors:Origins</c> en appsettings.
/// El constructor sin argumentos existe para que <c>IOptions&lt;T&gt;</c> pueda construirlo.
/// </summary>
public sealed record CorsSettings(string[] Origins)
{
    public CorsSettings() : this([]) { }
}
