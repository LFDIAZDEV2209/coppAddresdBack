namespace CoppAddresd.Application.Common.Storage;

/// <summary>Reserva fotos de comida frente a firmas o lecturas genéricas, incluso por alias.</summary>
public static class FoodPhotoStorageKeys
{
    public static bool IsProtected(string key)
    {
        var decoded = key;
        for (var i = 0; i < 8; i++)
        {
            var next = Uri.UnescapeDataString(decoded);
            if (next == decoded) break;
            decoded = next;
        }
        return decoded.Replace('\\', '/').Split('/')
            .Any(segment => segment.Equals("foodai", StringComparison.OrdinalIgnoreCase));
    }
    public static void RequireGenericKey(string key)
    {
        if (IsProtected(key)) throw new UnauthorizedAccessException("La foto requiere acceso clínico autorizado.");
    }
    public static void RequirePhotoKey(string key)
    {
        if (!key.StartsWith("foodai/", StringComparison.Ordinal) || key.IndexOfAny(['\\', '%', ':', '?', '#']) >= 0
            || key.Split('/').Any(s => s is "" or "." or ".."))
            throw new UnauthorizedAccessException("La clave de foto no es canónica.");
    }
}
