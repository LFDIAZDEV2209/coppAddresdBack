using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using CoppAddresd.Application.Features.Media;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>Validación real y conversión antes de escribir S3. Cuotas durables por
/// usuario/día UTC, serializadas por advisory lock; ninguna E/S externa en la transacción.</summary>
public sealed class ExerciseMediaPreparation(IObjectStorageService storage, IMediaItemRepository repository,
    AppDbContext db, IConfiguration config, IHostEnvironment environment, ILogger<ExerciseMediaPreparation> logger) : IExerciseMediaUpload
{
    public const long MaxVideoBytes = 20L * 1024 * 1024;
    public const long MaxImageBytes = 5L * 1024 * 1024;
    private static readonly SemaphoreSlim Slots = new(2, 2);

    public async Task<MediaItemDto> UploadAsync(Stream file, long length, string title, string kind, Guid actorId, CancellationToken ct)
    {
        if (kind is not ("Video" or "Image") || length <= 0 || length > (kind == "Video" ? MaxVideoBytes : MaxImageBytes))
            throw new UnprocessableEntityException("El vídeo admite hasta 20 MB y la imagen hasta 5 MB.");
        var temp = Path.Combine(Path.GetTempPath(), "copp-exercise-upload-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(110));
        try
        {
            var input = Path.Combine(temp, "original");
            await using (var target = File.Create(input))
            {
                var buffer = new byte[81920]; long bytes = 0; int read;
                while ((read = await file.ReadAsync(buffer, timeout.Token)) > 0)
                {
                    bytes += read;
                    if (bytes > length) throw new UnprocessableEntityException("El tamaño del archivo supera el límite declarado.");
                    await target.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
                }
                if (bytes != length) throw new UnprocessableEntityException("La subida del archivo quedó incompleta.");
            }
            return await PrepareAsync(input, temp, title, "", kind, null, actorId, timeout.Token);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        { throw new UnprocessableEntityException("La preparación tardó demasiado. Intenta con un vídeo más corto.", ex); }
        finally { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
    }

    public async Task<MediaItemDto> PrepareAsync(string input, string temp, string title, string author, string kind,
        MediaProvenance? provenance, Guid actorId, CancellationToken ct, Guid? importJobId = null)
    {
        if (!storage.IsCloudStorage && !environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
            throw new UnprocessableEntityException("El almacenamiento S3 debe estar configurado antes de subir demostraciones en este entorno.");
        if (actorId == Guid.Empty) throw new UnprocessableEntityException("No se pudo identificar al usuario que sube el archivo.");
        if (title.Trim().Length is < 1 or > 200) throw new UnprocessableEntityException("El título debe tener entre 1 y 200 caracteres.");
        if (!await Slots.WaitAsync(0, ct)) throw new UnprocessableEntityException("Hay otros vídeos preparándose. Intenta nuevamente en un momento.");
        var uploaded = new List<string>(); var saved = false;
        try
        {
            await ValidateSignatureAsync(input, kind, ct);
            var details = await ProcessAsync("ffprobe", ["-v", "error", "-protocol_whitelist", "file,pipe", "-show_entries", "format=duration:stream=codec_type,width,height", "-of", "json", input], ct);
            using var probe = JsonDocument.Parse(details);
            if (!probe.RootElement.GetProperty("streams").EnumerateArray().Any(s => s.GetProperty("codec_type").GetString() == "video"))
                throw new UnprocessableEntityException("El archivo no contiene una imagen o un vídeo válido.");
            var visual = probe.RootElement.GetProperty("streams").EnumerateArray().First(s => s.GetProperty("codec_type").GetString() == "video");
            if (visual.GetProperty("width").GetInt32() > 4096 || visual.GetProperty("height").GetInt32() > 4096)
                throw new UnprocessableEntityException("La resolución máxima de entrada es de 4096 píxeles por lado.");
            double seconds = 0;
            if (kind == "Video" && (!probe.RootElement.TryGetProperty("format", out var format)
                || !format.TryGetProperty("duration", out var duration)
                || !double.TryParse(duration.ToString(), CultureInfo.InvariantCulture, out seconds) || seconds <= 0 || seconds > 180))
                throw new UnprocessableEntityException("El vídeo debe durar entre 1 y 180 segundos (3 minutos).");
            var output = Path.Combine(temp, kind == "Video" ? "demo.mp4" : "demo.png");
            var poster = Path.Combine(temp, "poster.jpg");
            if (kind == "Video")
                await FfmpegAsync(["-i", input, "-map", "0:v:0", "-an", "-vf", "scale=trunc(min(960\\,iw)/2)*2:-2", "-c:v", "libx264", "-threads", "2", "-preset", "fast", "-crf", "26", "-maxrate", "800k", "-bufsize", "1600k", "-pix_fmt", "yuv420p", "-movflags", "+faststart", output], ct);
            else await FfmpegAsync(["-i", input, "-frames:v", "1", "-vf", "scale=min(1600\\,iw):-1", output], ct);
            await FfmpegAsync(["-i", output, "-frames:v", "1", "-vf", "scale=min(960\\,iw):-1", poster], ct);
            var length = new FileInfo(output).Length;
            if (length > (kind == "Video" ? MaxVideoBytes : MaxImageBytes)) throw new UnprocessableEntityException("La copia final supera el límite de almacenamiento. Usa un vídeo más corto o una imagen más pequeña.");
            var posterLength = new FileInfo(poster).Length;
            if (posterLength > 1024 * 1024) throw new UnprocessableEntityException("La miniatura supera 1 MB. Usa otro archivo.");
            // Reserva durable ANTES del primer PUT: los fallos posteriores consumen cuota
            // para impedir escrituras/reintentos ilimitados. El registro no se borra con el medio.
            await ReserveAsync(actorId, length + posterLength, ct);
            var id = importJobId ?? Guid.NewGuid();
            var key = $"media/exercises/{id:N}/{Path.GetFileName(output)}";
            var thumbnail = $"media/exercises/{id:N}/poster.jpg";
            var contentType = kind == "Video" ? "video/mp4" : "image/png";
            await using (var stream = File.OpenRead(output)) { uploaded.Add(key); await storage.PutObjectAsync(key, stream, contentType, ct); }
            await using (var stream = File.OpenRead(poster)) { uploaded.Add(thumbnail); await storage.PutObjectAsync(thumbnail, stream, "image/jpeg", ct); }
            if (provenance is not null) provenance = provenance with { Changes = kind == "Video"
                ? "Convertido a MP4 H.264 sin audio; resolución y compresión adaptadas. Miniatura extraída del vídeo."
                : "Convertido a PNG; primer fotograma. Miniatura reducida." };
            var entity = new MediaItem { Id = id, Title = title.Trim(), Author = author, Description = "Demostración seleccionada por el equipo clínico.",
                MediaType = kind == "Video" ? MediaType.Video : MediaType.Image, Category = MediaCategory.SaludFisica,
                StorageKey = key, ThumbnailKey = thumbnail, ContentType = contentType, FileSizeBytes = length,
                DurationSecs = kind == "Video" ? (int?)Math.Ceiling(seconds) : null,
                Status = MediaStatus.Published, CreatedAt = DateTimeOffset.UtcNow, PublishedAt = DateTimeOffset.UtcNow,
                CreatedBy = actorId, Provenance = provenance };
            await repository.AddAsync(entity, ct); saved = true;
            return MediaItemDto.FromEntity(entity);
        }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                if (!saved) foreach (var key in uploaded)
                    try { await storage.DeleteObjectAsync(key, cleanup.Token); }
                    catch (Exception ex) { logger.LogWarning(ex, "No se pudo limpiar una demostración fallida"); }
            }
            finally { Slots.Release(); }
        }
    }

    private async Task ReserveAsync(Guid user, long bytes, CancellationToken ct)
    {
        var limitCount = Math.Clamp(config.GetValue("ExerciseMedia:DailyUploadLimit", 10), 1, 100);
        var limitBytes = Math.Clamp(config.GetValue("ExerciseMedia:DailyStorageMegabytes", 100), 20, 1000) * 1024L * 1024;
        var start = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        var id = Guid.NewGuid();
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"exercise-media:" + user}, 0))", ct);
            if (!await db.Set<ExerciseMediaUsage>().AnyAsync(x => x.Id == id, ct))
            {
                var used = await db.Set<ExerciseMediaUsage>().Where(x => x.UserId == user && x.CreatedAt >= start)
                    .GroupBy(x => x.UserId).Select(g => new { Count = g.Count(), Bytes = g.Sum(x => x.Bytes) }).SingleOrDefaultAsync(ct);
                if ((used?.Count ?? 0) >= limitCount || (used?.Bytes ?? 0) + bytes > limitBytes)
                    throw new UnprocessableEntityException($"Alcanzaste el límite diario de {limitCount} archivos o {limitBytes / 1024 / 1024} MB. Se renueva a las 00:00 UTC. Puedes reutilizar la biblioteca o usar YouTube.");
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO app.exercise_media_usage (id, user_id, bytes, created_at) VALUES ({id}, {user}, {bytes}, {DateTimeOffset.UtcNow})", ct);
            }
            await tx.CommitAsync(ct);
        });
    }
    private static async Task ValidateSignatureAsync(string input, string kind, CancellationToken ct)
    {
        var signature = new byte[12];
        await using var file = File.OpenRead(input);
        var count = await file.ReadAsync(signature, ct);
        var hex = Convert.ToHexString(signature);
        var image = hex.StartsWith("89504E470D0A1A0A", StringComparison.Ordinal)
            || hex.StartsWith("FFD8FF", StringComparison.Ordinal)
            || System.Text.Encoding.ASCII.GetString(signature, 0, 6) is "GIF87a" or "GIF89a"
            || (System.Text.Encoding.ASCII.GetString(signature, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(signature, 8, 4) == "WEBP");
        var video = System.Text.Encoding.ASCII.GetString(signature, 4, 4) == "ftyp"
            || hex.StartsWith("1A45DFA3", StringComparison.Ordinal);
        if (count < 12 || !(kind == "Video" ? video : image))
            throw new UnprocessableEntityException("Usa un archivo MP4, MOV, WebM, JPG, PNG, WebP o GIF válido.");
    }

    private static Task<string> FfmpegAsync(string[] args, CancellationToken ct) => ProcessAsync("ffmpeg",
        ["-nostdin", "-hide_banner", "-loglevel", "error", "-threads", "2", "-filter_threads", "1", "-protocol_whitelist", "file,pipe", .. args], ct);
    private static async Task<string> ProcessAsync(string executable, string[] args, CancellationToken ct)
    {
        var start = new ProcessStartInfo(executable) { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = start };
        try { process.Start(); }
        catch (System.ComponentModel.Win32Exception ex) { throw new UnprocessableEntityException("El servidor no tiene habilitada la preparación de vídeos. Contacta al administrador.", ex); }
        using var registration = ct.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } });
        var errors = process.StandardError.ReadToEndAsync(ct);
        var output = process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct); await errors;
        if (process.ExitCode != 0) throw new UnprocessableEntityException("No se pudo preparar este archivo. Selecciona otra demostración.");
        return await output;
    }
}
