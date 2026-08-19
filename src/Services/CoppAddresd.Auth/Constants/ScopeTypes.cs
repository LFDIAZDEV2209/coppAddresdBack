namespace CoppAddresd.Auth.Constants;

/// <summary>
/// Tipos de scope de autorización. El modelo de permisos por contexto es
/// jerárquico: una asignación en un nivel más específico domina sobre el
/// ancestral (Location &gt; Clinic &gt; Organization &gt; Global). Los niveles
/// futuros (Department, CareTeam, Patient) se agregan como nuevos valores sin
/// cambiar la estructura.
/// </summary>
public static class ScopeTypes
{
    public const string Global = "Global";
    public const string Organization = "Organization";
    public const string Clinic = "Clinic";
    public const string Location = "Location";

    /// <summary>Orden de especificidad (mayor = más específico).</summary>
    private static readonly Dictionary<string, int> Specificity = new()
    {
        [Location] = 3,
        [Clinic] = 2,
        [Organization] = 1,
        [Global] = 0,
    };

    public static bool IsValid(string scopeType)
        => Specificity.ContainsKey(scopeType);

    /// <summary>true si <paramref name="candidate"/> es igual o más específico que <paramref name="baseline"/>.</summary>
    public static bool IsAtLeastAsSpecific(string candidate, string baseline)
        => Specificity[candidate] >= Specificity[baseline];
}