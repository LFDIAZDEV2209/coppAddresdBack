using CoppAddresd.Application.Features.Media;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace CoppAddresd.Infrastructure.Services;

public sealed class ExerciseMediaImportQueue(AppDbContext db, IMediaItemRepository media) : IExerciseMediaImportQueue
{
    public async Task<ExerciseMediaImportStatus> EnqueueAsync(ImportExerciseMediaRequest request, Guid userId, CancellationToken ct)
    {
        if (request.Id <= 0 || request.Kind is not ("Video" or "Image") || userId == Guid.Empty)
            throw new UnprocessableEntityException("Selecciona una imagen o un vídeo válido de wger.");
        var id = Guid.NewGuid();
        var start = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"exercise-media-job:" + userId}, 0))", ct);
            var jobs = db.Set<ExerciseMediaImportJob>();
            var existing = await jobs.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId && x.SourceId == request.Id && x.Kind == request.Kind && (x.Status == "queued" || x.Status == "processing"), ct);
            if (existing is not null) { await tx.CommitAsync(ct); return new ExerciseMediaImportStatus(existing.Id, existing.Status); }
            var completed = await jobs.AsNoTracking().Where(x => x.UserId == userId && x.SourceId == request.Id && x.Kind == request.Kind && x.Status == "completed")
                .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
            if (completed?.MediaId is { } completedId)
            {
                var saved = await media.GetByIdAsync(completedId, ct);
                if (saved?.Status == CoppAddresd.Domain.Enums.MediaStatus.Published)
                { await tx.CommitAsync(ct); return new ExerciseMediaImportStatus(completed!.Id, "completed", MediaItemDto.FromEntity(saved)); }
            }
            if (await jobs.CountAsync(x => x.UserId == userId && x.CreatedAt >= start, ct) >= 10
                || await jobs.CountAsync(x => x.UserId == userId && (x.Status == "queued" || x.Status == "processing"), ct) >= 2)
                throw new UnprocessableEntityException("Puedes preparar hasta 10 importaciones al día y 2 a la vez. Reutiliza los archivos de la biblioteca.");
            // ID estable en caso de que se reintente una confirmación de commit.
            if (!await jobs.AnyAsync(x => x.Id == id, ct))
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO app.exercise_media_import_jobs (id,user_id,source_id,kind,status,attempts,created_at,updated_at) VALUES ({id},{userId},{request.Id},{request.Kind},{"queued"},0,{DateTimeOffset.UtcNow},{DateTimeOffset.UtcNow})", ct);
            await tx.CommitAsync(ct);
            return new ExerciseMediaImportStatus(id, "queued");
        });
    }
    public async Task<ExerciseMediaImportStatus?> GetAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var job = await db.Set<ExerciseMediaImportJob>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (job is null) return null;
        var item = job.MediaId is { } mediaId ? await media.GetByIdAsync(mediaId, ct) : null;
        return new(job.Id, job.Status, item is null ? null : MediaItemDto.FromEntity(item), job.Error);
    }
}
