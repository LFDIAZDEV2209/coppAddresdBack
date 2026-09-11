using System.Security.Claims;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.DTOs.LabExam;
using CoppAddresd.Application.Exceptions;
using CoppAddresd.Application.Features.LabExam;
using CoppAddresd.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Api.Controllers;

/// <summary>
/// Endpoint para la subida de exámenes de laboratorio (imágenes o PDF) desde el chat móvil.
/// </summary>
[ApiController]
[Route("api/v1/lab-exams")]
[Route("api/v1/chat/lab-exam")]
[Authorize]
public class LabExamController(
    IMediator mediator,
    ILogger<LabExamController> logger) : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".pdf"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "application/pdf"
    };

    private const long MaxRawSizeBytes = 20 * 1024 * 1024; // 20 MB

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<LabExamUploadResult>> Upload(
        [FromForm] IFormFile? file,
        [FromForm] string? threadId = null,
        [FromForm] string? language = null,
        CancellationToken ct = default)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var patientId))
        {
            return Unauthorized(new { error = new { code = "UNAUTHORIZED", message = "Usuario no autenticado." } });
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = new { code = "EMPTY_FILE", message = "No se recibió un archivo o el archivo está vacío." } });
        }

        var extension = Path.GetExtension(file.FileName);
        var normalizedContentType = file.ContentType?.Split(';')[0].Trim();

        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension) ||
            string.IsNullOrWhiteSpace(normalizedContentType) || !AllowedContentTypes.Contains(normalizedContentType))
        {
            return StatusCode(StatusCodes.Status422UnprocessableEntity, new
            {
                error = new
                {
                    code = "UNSUPPORTED_FILE_TYPE",
                    message = "Unsupported file type. Please upload a JPEG, PNG, or PDF."
                }
            });
        }

        if (file.Length > MaxRawSizeBytes)
        {
            return StatusCode(StatusCodes.Status422UnprocessableEntity, new
            {
                error = new
                {
                    code = "FILE_TOO_LARGE",
                    message = "File exceeds the 20 MB limit."
                }
            });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var command = new UploadLabExamCommand(
                stream,
                file.FileName,
                file.ContentType ?? "application/octet-stream",
                file.Length,
                patientId,
                threadId,
                language);

            var result = await mediator.Send(command, ct);
            return Ok(result);
        }
        catch (ValidationException ex)
        {
            logger.LogWarning("Validación de UploadLabExamCommand falló: {Message}", ex.Message);
            var is422 = ex.Errors.Any(e =>
                e.ErrorMessage == "Unsupported file type. Please upload a JPEG, PNG, or PDF." ||
                e.ErrorMessage == "File exceeds the 20 MB limit.");
            if (is422)
            {
                var first = ex.Errors.First(e =>
                    e.ErrorMessage == "Unsupported file type. Please upload a JPEG, PNG, or PDF." ||
                    e.ErrorMessage == "File exceeds the 20 MB limit.");
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new
                {
                    error = new { code = "UNPROCESSABLE_ENTITY", message = first.ErrorMessage }
                });
            }

            return BadRequest(new
            {
                error = new { code = "VALIDATION_ERROR", message = "Validation failed.", errors = ex.Errors.Select(e => e.ErrorMessage) }
            });
        }
        catch (NotFoundException ex)
        {
            logger.LogWarning("Paciente no encontrado al procesar examen de laboratorio: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status404NotFound, new
            {
                error = new
                {
                    code = "PATIENT_NOT_FOUND",
                    message = ex.Message
                }
            });
        }
        catch (UnprocessableEntityException ex)
        {
            logger.LogWarning("Examen de laboratorio no procesable: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status422UnprocessableEntity, new
            {
                error = new
                {
                    code = "UNPROCESSABLE_ENTITY",
                    message = ex.Message
                }
            });
        }
        catch (AiServiceException ex)
        {
            logger.LogError(ex, "AI service falló al extraer métricas del examen (status {StatusCode}): {Detail}",
                ex.StatusCode, ex.Detail);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = new
                {
                    code = "AI_SERVICE_UNAVAILABLE",
                    message = "El servicio de IA no pudo procesar el examen de laboratorio."
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error inesperado al procesar examen de laboratorio");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = new
                {
                    code = "INTERNAL_ERROR",
                    message = "No fue posible procesar la solicitud."
                }
            });
        }
    }
}
