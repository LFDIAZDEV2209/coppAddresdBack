using CoppAddresd.Application.Features.Media;

namespace CoppAddresd.Application.Interfaces;

public sealed record ExerciseMediaCandidate(int Id, int ExerciseId, string Kind, string Title,
    string Author, string SourceUrl, string LicenseName, string LicenseUrl, string? PreviewUrl);
public sealed record ExerciseMediaPage(IReadOnlyList<ExerciseMediaCandidate> Items, int Page, bool HasMore);
public sealed record ImportExerciseMediaRequest(int Id, string Kind, Guid? JobId = null);

/// <summary>Catálogo público y copia autorizada de wger, sin datos de pacientes.</summary>
public interface IExerciseMediaSource
{
    Task<ExerciseMediaPage> SearchAsync(string? search, int page, CancellationToken ct);
    Task<MediaItemDto> ImportAsync(ImportExerciseMediaRequest request, Guid? actorId, CancellationToken ct);
}
