namespace CoppAddresd.Application.Interfaces;

/// <summary>Traduce texto de presentación sin alterar el contenido clínico persistido.</summary>
public interface IContentTranslationService
{
    Task<IReadOnlyDictionary<string, string>> TranslateAsync(
        IReadOnlyCollection<string> sourceTexts,
        string language,
        CancellationToken ct);
}
