namespace CoppAddresd.Application.Common.Storage;

/// <summary>Espacio nuevo y privado de originales/sidecars. Los consumidores
/// genéricos no pueden leer, firmar, copiar, alterar ni enumerar estos objetos.</summary>
public static class DocumentArchiveStorageKeys
{
    public static bool IsProtected(string? key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        // Rechazo conservador de alias URL, además de alias del filesystem.
        var decoded = key;
        for (var count = 0; count < 8; count++)
        {
            var next = Uri.UnescapeDataString(decoded);
            if (next == decoded) break;
            decoded = next;
        }
        var raw = decoded.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        static bool ArchiveRoot(IReadOnlyList<string> parts) => parts.Count >= 3
            && parts[0].Equals("documents", StringComparison.OrdinalIgnoreCase)
            && (parts.Count >= 4 || Guid.TryParse(parts[2], out _));
        if (ArchiveRoot(raw)) return true;
        var normalized = new List<string>();
        foreach (var segment in raw)
        {
            if (segment == ".") continue;
            if (segment == "..") { if (normalized.Count > 0) normalized.RemoveAt(normalized.Count - 1); }
            else normalized.Add(segment);
        }
        return ArchiveRoot(normalized);
    }

    public static void RequireGenericKey(string? key)
    {
        if (IsProtected(key)) throw new UnauthorizedAccessException("El archivo documental requiere un flujo autorizado explícito.");
    }

    public static void RequireArchiveKey(string key)
    {
        var parts = key.Split('/');
        if (parts.Length != 4 || parts[0] != "documents"
            || !Guid.TryParseExact(parts[1], "D", out var patient)
            || !Guid.TryParseExact(parts[2], "D", out var document)
            || key != $"documents/{patient:D}/{document:D}/{parts[3]}"
            || parts[3].IndexOfAny(['\\', '%', ':', '?', '#']) >= 0
            || !(parts[3] == "extraction.json" || parts[3].StartsWith("original.", StringComparison.Ordinal) && parts[3].Length > 9))
            throw new UnauthorizedAccessException("La clave del archivo documental no es canónica.");
    }
}
