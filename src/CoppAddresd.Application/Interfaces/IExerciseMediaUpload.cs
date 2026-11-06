using CoppAddresd.Application.Features.Media;
namespace CoppAddresd.Application.Interfaces;
public interface IExerciseMediaUpload
{
    Task<MediaItemDto> UploadAsync(Stream file, long length, string title, string kind, Guid actorId, CancellationToken ct);
}
