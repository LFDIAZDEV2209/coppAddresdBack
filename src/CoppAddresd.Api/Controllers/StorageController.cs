using CoppAddresd.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

/// <summary>
/// Endpoint de escritura directa de objetos en el storage. Funciona como
/// destino del "presigned URL" con el proveedor Local; con S3 la subida irá
/// directo al bucket y este endpoint solo persiste la metadata en /media.
/// </summary>
[ApiController]
[Route("api/v1/storage")]
[Authorize]
public class StorageController(IObjectStorageService objectStorage) : ControllerBase
{
    /// <summary>Almacena el body crudo bajo la clave indicada (semántica PUT de S3).</summary>
    [HttpPut("{**key}")]
    public async Task<IActionResult> Put(string key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key))
            return BadRequest(new { message = "La clave del objeto es requerida." });

        var contentType = Request.ContentType;
        await objectStorage.PutObjectAsync(key, Request.Body, contentType, ct);
        return NoContent();
    }

    /// <summary>Obtiene el contenido del objeto almacenado.</summary>
    [HttpGet("{**key}")]
    public async Task<IActionResult> Get(string key, CancellationToken ct)
    {
        try
        {
            var stream = await objectStorage.GetObjectAsync(key, ct);
            return File(stream, "application/octet-stream", enableRangeProcessing: true);
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { message = "El objeto no existe." });
        }
    }
}
