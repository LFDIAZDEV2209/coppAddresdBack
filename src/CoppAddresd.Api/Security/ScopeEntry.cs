namespace CoppAddresd.Api.Security;

/// <summary>
/// Un nivel de la cadena de contextos de autorización. Se ordena de más
/// específica a menos específica y termina en Global. La API construye la
/// cadena desde el contexto activo (clínica → organización → global) y la
/// envía al Auth Service para la introspección.
/// </summary>
public record ScopeEntry(string ScopeType, Guid? ScopeId)
{
    public static ScopeEntry Global => new("Global", null);

    public static string EncodeChain(IEnumerable<ScopeEntry> chain)
        => string.Join("|", chain.Select(s =>
            s.ScopeId is null ? s.ScopeType : $"{s.ScopeType}:{s.ScopeId}"));
}
