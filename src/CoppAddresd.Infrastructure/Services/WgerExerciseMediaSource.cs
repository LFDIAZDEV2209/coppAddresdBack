using System.Net.Http.Json;
using System.Text.Json;
using CoppAddresd.Application.Features.Media;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Exceptions;
using Microsoft.Extensions.Caching.Memory;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>Importación explícita: URLs resueltas por wger, límites de bytes/tiempo,
/// FFmpeg sin red, MP4 H.264 y créditos inmutables. No descarga YouTube.</summary>
public sealed class WgerExerciseMediaSource(HttpClient http,
    ExerciseMediaPreparation preparation, IMemoryCache cache)
    : IExerciseMediaSource
{
    private static readonly SemaphoreSlim ImportSlots = new(2, 2);
    private const long MaxBytes = 128L * 1024 * 1024;
    private static readonly Dictionary<int, (string Name, string Url)> Licenses = new()
    {
        [1] = ("CC BY-SA 3.0", "https://creativecommons.org/licenses/by-sa/3.0/"),
        [2] = ("CC BY-SA 4.0", "https://creativecommons.org/licenses/by-sa/4.0/"),
        [3] = ("CC0 1.0", "https://creativecommons.org/publicdomain/zero/1.0/"),
        [4] = ("CC BY 4.0", "https://creativecommons.org/licenses/by/4.0/"),
    };

    public async Task<ExerciseMediaPage> SearchAsync(string? search, int page, CancellationToken ct)
    {
        page = Math.Clamp(page, 1, 100);
        search = (search ?? "").Trim();
        if (search.Length > 100) throw new UnprocessableEntityException("La búsqueda admite hasta 100 caracteres.");
        var key = $"exercise-media:wger:v1:{page}:{search}";
        if (cache.TryGetValue<ExerciseMediaPage>(key, out var cached)) return cached!;
        try
        {
            var data = await JsonAsync($"exerciseinfo/?limit=20&offset={(page - 1) * 20}&name__search={Uri.EscapeDataString(search)}", ct);
            var items = new List<ExerciseMediaCandidate>();
            foreach (var exercise in data.GetProperty("results").EnumerateArray())
            {
                var name = Name(exercise);
                foreach (var (field, kind) in new[] { ("videos", "Video"), ("images", "Image") })
                {
                    if (!exercise.TryGetProperty(field, out var media)) continue;
                    foreach (var item in media.EnumerateArray())
                    {
                        var candidate = Candidate(item, exercise.GetProperty("id").GetInt32(), name, kind);
                        if (candidate is not null) items.Add(candidate);
                    }
                }
            }
            var result = new ExerciseMediaPage(items, page, data.TryGetProperty("next", out var next) && next.ValueKind == JsonValueKind.String);
            cache.Set(key, result, TimeSpan.FromMinutes(10));
            return result;
        }
        catch (HttpRequestException ex) { throw Unavailable(ex); }
        catch (JsonException ex) { throw Unavailable(ex); }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested) { throw Unavailable(ex); }
    }

    public async Task<MediaItemDto> ImportAsync(ImportExerciseMediaRequest request, Guid? actorId, CancellationToken ct)
    {
        if (request.Id <= 0 || request.Kind is not ("Video" or "Image"))
            throw new UnprocessableEntityException("Selecciona una imagen o un vídeo de wger.");
        if (!await ImportSlots.WaitAsync(0, ct))
            throw new UnprocessableEntityException("Hay otras demostraciones preparándose. Intenta nuevamente en un momento.");
        var temp = Path.Combine(Path.GetTempPath(), "copp-exercise-" + Guid.NewGuid().ToString("N"));
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(300));
        var token = deadline.Token;
        try
        {
            var item = await JsonAsync($"{(request.Kind == "Video" ? "video" : "exerciseimage")}/{request.Id}/", token);
            var exerciseId = item.GetProperty("exercise").GetInt32();
            var exercise = await JsonAsync($"exerciseinfo/{exerciseId}/", token);
            var candidate = Candidate(item, exerciseId, Name(exercise), request.Kind)
                ?? throw new UnprocessableEntityException("Este recurso no tiene licencia o créditos suficientes para importarlo.");
            var upstreamLicense = await JsonAsync($"license/{item.GetProperty("license").GetInt32()}/", token);
            static string NormalizeLicense(string value) => value.Replace("http://", "https://", StringComparison.Ordinal).Replace("/deed.en", "/", StringComparison.Ordinal).TrimEnd('/');
            if (!string.Equals(NormalizeLicense(Text(upstreamLicense, "url")), NormalizeLicense(candidate.LicenseUrl), StringComparison.OrdinalIgnoreCase))
                throw new UnprocessableEntityException("La licencia de este recurso cambió. Selecciona otra demostración.");
            var original = Text(item, request.Kind == "Video" ? "video" : "image");
            if (!AllowedAsset(original)) throw new UnprocessableEntityException("La fuente no ofrece un archivo de wger válido.");
            Directory.CreateDirectory(temp);
            var input = Path.Combine(temp, "original");
            await DownloadAsync(original, input, token);
            var provenance = new MediaProvenance("wger", $"{request.Kind}:{request.Id}", candidate.Title,
                candidate.Author, SafeLink(Text(item, "license_author_url")), candidate.SourceUrl,
                candidate.LicenseName, candidate.LicenseUrl,
                request.Kind == "Video" ? "Convertido a MP4 H.264 sin audio; movimiento sin cambios. Miniatura extraída del vídeo."
                    : "Convertido a PNG; primer fotograma. Miniatura reducida.",
                original, item.GetRawText(), DateTimeOffset.UtcNow, SafeLink(Text(item, "license_derivative_source_url")));
            return await preparation.PrepareAsync(input, temp, candidate.Title, candidate.Author,
                request.Kind, provenance, actorId ?? Guid.Empty, token, request.JobId);
        }
        catch (HttpRequestException ex) { throw Unavailable(ex); }
        catch (JsonException ex) { throw Unavailable(ex); }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested) { throw new UnprocessableEntityException("La preparación tardó demasiado. Intenta nuevamente.", ex); }
        finally
        {
            try { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
            finally { ImportSlots.Release(); }
        }
    }

    private async Task<JsonElement> JsonAsync(string path, CancellationToken ct)
    {
        using var response = await http.GetAsync("https://wger.de/api/v2/" + path, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        // Los endpoints utilizados tienen páginas acotadas; también se limita la respuesta física.
        await response.Content.LoadIntoBufferAsync(8 * 1024 * 1024, ct);
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
    }

    private async Task DownloadAsync(string url, string path, CancellationToken ct)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaxBytes) throw new UnprocessableEntityException("El archivo de wger supera 128 MB.");
        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var target = File.Create(path);
        var buffer = new byte[81920]; long size = 0; int count;
        while ((count = await source.ReadAsync(buffer, ct)) > 0)
        {
            size += count;
            if (size > MaxBytes) throw new UnprocessableEntityException("El archivo de wger supera 128 MB.");
            await target.WriteAsync(buffer.AsMemory(0, count), ct);
        }
    }

    private static ExerciseMediaCandidate? Candidate(JsonElement item, int exerciseId, string name, string kind)
    {
        if (!item.TryGetProperty("license", out var license) || !license.TryGetInt32(out var licenseId) || !Licenses.TryGetValue(licenseId, out var terms)) return null;
        var author = Text(item, "license_author");
        if (item.TryGetProperty("author_history", out var history))
            author = string.Join(", ", new[] { author }.Concat(history.EnumerateArray().Select(a => a.GetString() ?? ""))
                .Where(a => !string.IsNullOrWhiteSpace(a)).Distinct(StringComparer.Ordinal));
        if (licenseId != 3 && string.IsNullOrWhiteSpace(author)) return null;
        var title = Text(item, "license_title");
        return new(item.GetProperty("id").GetInt32(), exerciseId, kind, string.IsNullOrWhiteSpace(title) ? name : title,
            author, SafeLink(Text(item, "license_object_url")) ?? $"https://wger.de/en/exercise/{exerciseId}/view/",
            terms.Name, terms.Url, kind == "Image" && AllowedAsset(Text(item, "image")) ? Text(item, "image") : null);
    }
    private static string Name(JsonElement exercise)
    {
        if (exercise.TryGetProperty("translations", out var translations))
            foreach (var language in new[] { 4, 2 })
                foreach (var translation in translations.EnumerateArray())
                    if (translation.GetProperty("language").GetInt32() == language) return Text(translation, "name");
        return "wger #" + exercise.GetProperty("id").GetInt32();
    }
    private static string Text(JsonElement item, string name) => item.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value.ToString() : "";
    private static string? SafeLink(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == "https" || uri.Scheme == "http") && uri.UserInfo == "" ? uri.AbsoluteUri : null;
    private static bool AllowedAsset(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && uri.Host == "wger.de" && uri.IsDefaultPort && uri.UserInfo == ""
        && (uri.AbsolutePath.StartsWith("/media/exercise-video/", StringComparison.Ordinal) || uri.AbsolutePath.StartsWith("/media/exercise-images/", StringComparison.Ordinal));
    private static UnprocessableEntityException Unavailable(Exception ex) => new("No se pudo consultar wger. Puedes subir un archivo propio o usar YouTube mientras tanto.", ex);
}
