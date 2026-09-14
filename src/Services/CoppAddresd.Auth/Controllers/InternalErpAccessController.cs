using CoppAddresd.Auth.Authorization;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Auth.Controllers;

/// <summary>Contrato interno de suspensión ERP. El API valida permiso y alcance del actor público.</summary>
[ApiController]
[Route("api/auth/internal/erp-access")]
[AllowAnonymous]
[RequireInternalKey]
public sealed class InternalErpAccessController(AuthDbContext db) : ControllerBase
{
    [HttpPut("{userId:guid}")]
    public async Task<IActionResult> Change(
        Guid userId,
        ChangeErpAccessRequest request,
        CancellationToken ct
    )
    {
        try
        {
            return Ok(
                await new ErpAccessService(db).ChangeAsync(
                    request.OperationId,
                    userId,
                    request.EmployeeId,
                    request.Status,
                    ct
                )
            );
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
        catch (InvalidOperationException e)
        {
            return Conflict(new { message = e.Message });
        }
        catch (DbUpdateException)
        {
            return Conflict(
                new
                {
                    message = "El acceso cambió simultáneamente. Consulta su estado antes de reintentar.",
                }
            );
        }
    }

    [HttpGet("pending")]
    public async Task<IActionResult> Pending(CancellationToken ct) =>
        Ok(await new ErpAccessService(db).PendingAsync(ct));

    [HttpPost("pending")]
    public async Task<IActionResult> PendingFor(Guid[] employeeIds, CancellationToken ct)
    {
        if (employeeIds.Length > 100)
            return BadRequest();
        return Ok(await new ErpAccessService(db).PendingForAsync(employeeIds, ct));
    }

    [HttpPost("{operationId:guid}/complete")]
    public async Task<IActionResult> Complete(Guid operationId, CancellationToken ct)
    {
        await new ErpAccessService(db).CompleteAsync(operationId, ct);
        return NoContent();
    }
}

public sealed record ChangeErpAccessRequest(Guid OperationId, Guid EmployeeId, string Status);
