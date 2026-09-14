namespace CoppAddresd.Auth.Domain.Avatar;

/// <summary>Contrato de IDs estables. No guarda rutas ni información clínica.</summary>
public static class AvatarCatalog
{
    public static bool Accepts(string gender, string slot, string? id) => id is null || (slot, id) switch
    {
        ("shirt", "shirt-basic-01" or "shirt-basic-01-navy") => true,
        ("hair", "hair-02") => true,
        ("hair", "hair-03") => gender == "male",
        ("hair", "female-hair-long-01" or "female-hair-long-02" or "female-hair-long-03") => gender == "female",
        ("glasses", "glasses-01") => true,
        ("watch", "watch-01") => true,
        ("bracelet", "bracelet-01") => true,
        _ => false,
    };

    public static string DefaultGender(string? profileGender) => profileGender?.Trim().ToLowerInvariant() switch
    {
        "f" or "female" or "femenino" or "femenina" or "mujer" => "female",
        _ => "male",
    };
}
