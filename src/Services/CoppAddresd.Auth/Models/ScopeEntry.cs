namespace CoppAddresd.Auth.Models;

/// <summary>
/// Un nivel de la cadena de contextos de una autorización. La cadena se
/// ordena de más específica a menos específica y termina siempre en Global
/// (ej. Clinic:abc123 → Organization:org1 → Global). El llamador (ERP) conoce
/// la jerarquía de clínicas y la construye; el Auth Service solo evalúa.
/// </summary>
public record ScopeEntry(string ScopeType, Guid? ScopeId)
{
    public static readonly ScopeEntry Global = new(Constants.ScopeTypes.Global, null);

    /// <summary>Codifica la cadena como texto para logs/cache (Clinic:id|Organization:id|Global).</summary>
    public static string EncodeChain(IEnumerable<ScopeEntry> chain)
        => string.Join("|", chain.Select(s =>
            s.ScopeId is null ? s.ScopeType : $"{s.ScopeType}:{s.ScopeId}"));

    public override string ToString()
        => ScopeId is null ? ScopeType : $"{ScopeType}:{ScopeId}";
}