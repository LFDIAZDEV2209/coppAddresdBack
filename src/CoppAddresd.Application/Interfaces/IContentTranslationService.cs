namespace CoppAddresd.Application.Interfaces;

/// <summary>Identidad del destinatario validada por el servidor; no forma parte del payload al traductor.</summary>
public sealed record ContentTranslationSubject(string Scope, Guid UserId);

/// <summary>Traduce texto de presentación sin alterar el contenido clínico persistido.</summary>
public interface IContentTranslationService
{
    Task<IReadOnlyDictionary<string, string>> TranslateAsync(
        IReadOnlyCollection<string> sourceTexts,
        string language,
        CancellationToken ct,
        ContentTranslationSubject? subject = null);
}
