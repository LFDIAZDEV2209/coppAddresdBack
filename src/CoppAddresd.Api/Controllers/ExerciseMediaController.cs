using System.Security.Claims;
using CoppAddresd.Api.Authorization;
using CoppAddresd.Api.Constants;
using CoppAddresd.Application.Features.Media;
using CoppAddresd.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

[ApiController, Authorize, Route("api/v1/wellness/exercise-media")]
public sealed class ExerciseMediaController(IExerciseMediaSource source, IExerciseMediaUpload upload, IExerciseMediaImportQueue queue) : ControllerBase
{
    [HttpGet("wger"), RequirePermission(PermissionCodes.MediaView), RequirePermission(PermissionCodes.WellnessView)]
    public Task<ExerciseMediaPage> Search([FromQuery] string? search, [FromQuery] int page = 1, CancellationToken ct = default)
        => source.SearchAsync(search, page, ct);

    [HttpPost("uploads"), RequestSizeLimit(21 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 21 * 1024 * 1024)]
    [RequirePermission(PermissionCodes.MediaCreate), RequirePermission(PermissionCodes.MediaPublish), RequirePermission(PermissionCodes.WellnessManage)]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("exercise-media-upload")]
    public async Task<ActionResult<MediaItemDto>> Upload(IFormFile file, [FromForm] string title, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Forbid();
        var kind = file.ContentType.StartsWith("video/", StringComparison.Ordinal) ? "Video"
            : file.ContentType.StartsWith("image/", StringComparison.Ordinal) ? "Image" : "";
        await using var stream = file.OpenReadStream();
        var item = await upload.UploadAsync(stream, file.Length, title, kind, actor, ct);
        return Created($"/api/v1/media/{item.Id}", item);
    }

    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("exercise-media-upload")]
    [HttpPost("wger/import"), RequirePermission(PermissionCodes.MediaCreate), RequirePermission(PermissionCodes.MediaPublish), RequirePermission(PermissionCodes.WellnessManage)]
    public async Task<ActionResult<ExerciseMediaImportStatus>> Import(ImportExerciseMediaRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Forbid();
        var job = await queue.EnqueueAsync(request, actor, ct);
        return Accepted($"/api/v1/wellness/exercise-media/wger/imports/{job.JobId}", job);
    }

    [HttpGet("wger/imports/{id:guid}"), RequirePermission(PermissionCodes.WellnessManage), RequirePermission(PermissionCodes.MediaCreate)]
    public async Task<ActionResult<ExerciseMediaImportStatus>> GetImport(Guid id, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Forbid();
        var result = await queue.GetAsync(id, actor, ct);
        return result is null ? NotFound() : Ok(result);
    }
}
