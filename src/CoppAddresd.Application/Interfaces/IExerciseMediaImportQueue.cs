using CoppAddresd.Application.Features.Media;
namespace CoppAddresd.Application.Interfaces;
public sealed record ExerciseMediaImportStatus(Guid JobId, string Status, MediaItemDto? Media = null, string? Error = null);
public interface IExerciseMediaImportQueue
{
    Task<ExerciseMediaImportStatus> EnqueueAsync(ImportExerciseMediaRequest request, Guid userId, CancellationToken ct);
    Task<ExerciseMediaImportStatus?> GetAsync(Guid id, Guid userId, CancellationToken ct);
}
