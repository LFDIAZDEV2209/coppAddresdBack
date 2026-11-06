namespace CoppAddresd.Domain.Exceptions;

/// <summary>No hay una traducción válida; no sustituirla silenciosamente por otro idioma.</summary>
public sealed class ContentTranslationUnavailableException : Exception
{
    public ContentTranslationUnavailableException()
        : base("La traducción del contenido no está disponible. Intenta de nuevo.") { }
}
