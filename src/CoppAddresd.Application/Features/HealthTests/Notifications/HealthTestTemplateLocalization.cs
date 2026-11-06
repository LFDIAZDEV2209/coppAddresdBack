using System.Text.RegularExpressions;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums.HealthTests;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Exceptions;

namespace CoppAddresd.Application.Features.HealthTests.Notifications;

/// <summary>Traduce las plantillas redactadas antes de interpolar los identificadores del paciente.</summary>
internal static class HealthTestTemplateLocalization
{
    public static async Task<IReadOnlyDictionary<string, string>> PrepareAsync(
        IEnumerable<string> sources, NotificationLanguage language,
        IContentTranslationService? translations, CancellationToken ct)
    {
        var texts = sources.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.Ordinal).ToArray();
        if (language != NotificationLanguage.en || texts.Length == 0)
            return texts.ToDictionary(s => s, s => s, StringComparer.Ordinal);
        if (translations is null) throw new ContentTranslationUnavailableException();
        var translated = await translations.TranslateAsync(texts, "en", ct);
        foreach (var source in texts)
        {
            if (!translated.TryGetValue(source, out var target) || string.IsNullOrWhiteSpace(target)
                || !Placeholders(source).SequenceEqual(Placeholders(target)))
                throw new ContentTranslationUnavailableException();
        }
        return translated;
    }

    public static IEnumerable<string> AuthoredLabels(HealthTestAlert alert, string template)
    {
        var labels = new List<string?>();
        if (template.Contains("[test]", StringComparison.OrdinalIgnoreCase)) labels.Add(alert.Rule?.Name);
        if (template.Contains("[indicador]", StringComparison.OrdinalIgnoreCase)) labels.Add(alert.Result?.Label ?? alert.Rule?.Name);
        // Los códigos estables de indicadores son identificadores y se conservan entre idiomas.
        return labels.OfType<string>().Where(s => !string.IsNullOrWhiteSpace(s) && s != alert.Result?.Code);
    }

    public static string? DisplayLabel(string? source, NotificationLanguage language, IReadOnlyDictionary<string, string>? localized) =>
        language == NotificationLanguage.en && source is not null && localized is not null && localized.TryGetValue(source, out var value)
            ? value : source;

    private static IEnumerable<string> Placeholders(string source) =>
        Regex.Matches(source, @"\[[\w.-]+\]|\{\{?[^{}]+\}\}?", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))
            .Select(m => m.Value).Order(StringComparer.Ordinal);
}
