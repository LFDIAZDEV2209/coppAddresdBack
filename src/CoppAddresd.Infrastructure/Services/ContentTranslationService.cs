using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>Adaptador del traductor interno; nunca guarda ni sustituye las fuentes clínicas.</summary>
public sealed class ContentTranslationService(HttpClient client, IOptions<AiServiceSettings> settings)
    : IContentTranslationService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task<IReadOnlyDictionary<string, string>> TranslateAsync(
        IReadOnlyCollection<string> sourceTexts, string language, CancellationToken ct,
        ContentTranslationSubject? subject = null)
    {
        ct.ThrowIfCancellationRequested();
        if (sourceTexts is null) throw new ContentTranslationUnavailableException();
        var sources = sourceTexts.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToArray();
        if (language == "es") return sources.ToDictionary(x => x, x => x, StringComparer.Ordinal);
        if (language != "en" || sources.Any(x => x.Length > 8000)) throw new ContentTranslationUnavailableException();
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var batch = new List<Item>(); var characters = 0;
        foreach (var source in sources)
        {
            if (batch.Count == 64 || characters + source.Length > 32000)
            { await SendAsync(batch, result, ct, subject); batch.Clear(); characters = 0; }
            batch.Add(new(batch.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), source));
            characters += source.Length;
        }
        if (batch.Count > 0) await SendAsync(batch, result, ct, subject);
        return result;
    }

    private async Task SendAsync(List<Item> batch, Dictionary<string, string> result, CancellationToken ct,
        ContentTranslationSubject? subject)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                new Uri(new Uri(settings.Value.BaseUrl), "/internal/content/translate"));
            if (subject is not null)
                request.Options.Set(AiConsentSubject.Key, new(subject.Scope, UserId: subject.UserId, RequireRecipientConsent: true));
            request.Headers.Add("X-Internal-Key", settings.Value.InternalApiKey);
            request.Content = JsonContent.Create(new Request("en", batch.ToArray()), options: Json);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            using var response = await client.SendAsync(request, deadline.Token);
            ct.ThrowIfCancellationRequested();
            response.EnsureSuccessStatusCode();
            var translated = await response.Content.ReadFromJsonAsync<Response>(Json, deadline.Token);
            ct.ThrowIfCancellationRequested();
            if (translated?.Language != "en" || translated.Items is null || translated.Items.Length != batch.Count
                || translated.Items.Any(item => item is null || string.IsNullOrWhiteSpace(item.Id))
                || translated.Items.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != batch.Count)
                throw new ContentTranslationUnavailableException();
            var indexed = translated.Items.ToDictionary(x => x.Id, StringComparer.Ordinal);
            foreach (var item in batch)
            {
                if (!indexed.TryGetValue(item.Id, out var output) || string.IsNullOrWhiteSpace(output.Text)
                    || !ProtectedNumbers(item.Text).SequenceEqual(ProtectedNumbers(output.Text)))
                    throw new ContentTranslationUnavailableException();
                result[item.Text] = output.Text;
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && ex is HttpRequestException or JsonException or OperationCanceledException or UriFormatException)
        { throw new ContentTranslationUnavailableException(); }
    }

    private static IEnumerable<string> ProtectedNumbers(string text) => Regex.Matches(text, @"\d+(?:[.,]\d+)*")
        .Select(match => match.Value.Replace(',', '.')).Order(StringComparer.Ordinal);
    private sealed record Item(string Id, string Text);
    private sealed record Request([property: JsonPropertyName("target_language")] string Language, Item[] Items);
    private sealed record TranslatedItem(string Id, [property: JsonPropertyName("translation")] string Text);
    private sealed record Response([property: JsonPropertyName("target_language")] string Language, TranslatedItem[] Items);
}
