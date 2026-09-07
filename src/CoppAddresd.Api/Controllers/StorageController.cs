using CoppAddresd.Api.Security;
using CoppAddresd.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

/// <summary>
/// Endpoint de escritura directa de objetos en el storage. Funciona como
/// destino del "presigned URL" con el proveedor Local; con S3 la subida irá
/// directo al bucket y este endpoint solo persiste la metadata en /media.
/// La lectura se protege con URL firmada (sig+exp) porque el &lt;video&gt;/
/// &lt;audio&gt; del navegador no puede enviar el header Authorization.
/// </summary>
[ApiController]
[Route("api/v1/storage")]
[Authorize]
public class StorageController(
    IObjectStorageService objectStorage,
    StorageSignatureService signatureService) : ControllerBase
{
    /// <summary>Almacena el body crudo bajo la clave indicada (semántica PUT de S3).
    /// Acepta el header Bearer (requests de la app/gateway) o una URL firmada vía <c>exp</c>+<c>sig</c>.</summary>
    [HttpPut("{**key}")]
    [AllowAnonymous]
    public async Task<IActionResult> Put(string key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key))
            return BadRequest(new { message = "La clave del objeto es requerida." });

        var exp = Request.Query["exp"].ToString();
        var sig = Request.Query["sig"].ToString();

        var isSigned = signatureService.Validate(key, sig, exp, DateTimeOffset.UtcNow);
        var hasBearer = User.Identity?.IsAuthenticated == true;

        if (!isSigned && !hasBearer)
            return Unauthorized(new { message = "Acceso denegado: se requiere una URL firmada o sesión." });

        var contentType = Request.ContentType;
        await objectStorage.PutObjectAsync(key, Request.Body, contentType, ct);
        return NoContent();
    }

    /// <summary>
    /// Genera una URL firmada temporal para leer el objeto sin header Bearer.
    /// Requiere autenticación para firmar; la URL resultante es autocontenida.
    /// Con el proveedor S3 la URL es un presigned URL real del bucket; con el
    /// proveedor Local es el proxy del backend con firma HMAC (sig+exp).
    /// </summary>
    [HttpGet("sign")]
    public async Task<ActionResult<object>> Sign(
        [FromQuery] string key,
        [FromQuery] int expiresInSeconds = 900,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return BadRequest(new { message = "La clave del objeto es requerida." });

        if (expiresInSeconds is <= 0 or > 86400)
            return BadRequest(new { message = "La expiración debe estar entre 1 y 86400 segundos." });

        if (objectStorage.IsCloudStorage)
        {
            var cloudUrl = await objectStorage.GetPreSignedUrlAsync(
                key, TimeSpan.FromSeconds(expiresInSeconds), ct);
            return Ok(new { url = cloudUrl, expiresInSeconds });
        }

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);
        var signature = signatureService.Sign(key, expiresAt);

        var url = $"{Request.Scheme}://{Request.Host}/api/v1/storage/{key}" +
                  $"?exp={expiresAt.ToUnixTimeSeconds()}&sig={signature}";

        return Ok(new
        {
            url,
            expiresInSeconds,
        });
    }

    /// <summary>
    /// Obtiene el contenido del objeto. Acepta el header Bearer (requests de la app)
    /// o una URL firmada vía <c>exp</c>+<c>sig</c> (reproducción en &lt;video&gt;/&lt;audio&gt;).
    /// </summary>
    [HttpGet("{**key}")]
    [AllowAnonymous]
    public async Task<IActionResult> Get(string key, CancellationToken ct)
    {
        var exp = Request.Query["exp"].ToString();
        var sig = Request.Query["sig"].ToString();

        var isSigned = signatureService.Validate(key, sig, exp, DateTimeOffset.UtcNow);
        var hasBearer = User.Identity?.IsAuthenticated == true;

        if (!isSigned && !hasBearer)
            return Unauthorized(new { message = "Acceso denegado: se requiere una URL firmada o sesión." });

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
