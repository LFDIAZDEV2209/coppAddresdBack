using CoppAddresd.Api.Authorization;
using CoppAddresd.Api.Constants;
using CoppAddresd.Api.Context;
using CoppAddresd.Application.Features.Sos;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

/// <summary>
/// SOS real — botón de pánico del paciente (change sos-panic-real, D8).
/// Activación/consulta/cancelación: exclusivas del paciente con JWT de la app
/// móvil (aud=app, política <see cref="SosPolicies.AppPatient"/>). Atención:
/// exclusiva de staff del ERP (aud=erp, política <see cref="SosPolicies.ErpStaff"/>)
/// con permiso <c>Sos.Alerts.Manage</c> y scope clínico. El catch-all YARP del
/// Gateway enruta aquí, pero la política de audiencia la aplica SIEMPRE este
/// API (defensa en profundidad). El número de destino y el texto del SMS
/// NUNCA viajan en el body (D3): el contacto sale del perfil y la plantilla
/// es 100% server-side.
/// </summary>
[ApiController]
[Route("api/v1/sos/alerts")]
[Authorize]
public class SosController(
    IMediator mediator,
    ISosActorContext actorContext,
    ICurrentContext context,
    ISosAlertRepository repository
) : ControllerBase
{
    /// <summary>Encabezado obligatorio de idempotencia (UUIDv4, REQ-SOS-01).</summary>
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    /// <summary>Cabecera opcional de dispositivo para la cuota por dispositivo (D7).</summary>
    private const string DeviceIdHeader = "X-Device-Id";

    /// <summary>
    /// Activa la alerta SOS del paciente autenticado. Idempotencia completa:
    /// 400 sin Idempotency-Key válida, 201 al crear, 200 en replay idéntico,
    /// 409 si el payload difiere o ya hay alerta activa, 422 sin contacto
    /// E.164 válido y 429 (Retry-After) ante rate-limit distribuido.
    /// </summary>
    [HttpPost]
    [Authorize(SosPolicies.AppPatient)]
    [ProducesResponseType(typeof(SosAlertDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(SosAlertDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<SosAlertDto>> Activate(
        [FromBody] ActivateSosAlertRequest request,
        CancellationToken ct
    )
    {
        // La clave viaja al command: el validador FluentValidation responde
        // 400 (ProblemDetails) si falta o no es UUIDv4 (REQ-SOS-01).
        Request.Headers.TryGetValue(IdempotencyKeyHeader, out var keyValues);
        var idempotencyKey = keyValues.ToString().Trim();

        Request.Headers.TryGetValue(DeviceIdHeader, out var deviceValues);
        var deviceId = deviceValues.ToString().Trim();
        if (deviceId.Length == 0)
        {
            deviceId = null;
        }

        // La identidad del paciente sale SIEMPRE del JWT (nunca del body).
        var patientId = await actorContext.ResolvePatientProfileIdAsync(ct);
        if (patientId is null)
        {
            return UnprocessableEntity(
                new
                {
                    message = "Configura un contacto de emergencia con teléfono válido antes de activar el SOS.",
                }
            );
        }

        var result = await mediator.Send(
            new ActivateSosAlertCommand(
                patientId.Value,
                idempotencyKey,
                request.Latitude,
                request.Longitude,
                request.AccuracyMeters,
                request.LocationCapturedAt,
                deviceId
            ),
            ct
        );

        switch (result.Outcome)
        {
            case SosActivationOutcome.Created:
                return CreatedAtAction(
                    nameof(GetAlert),
                    new { id = result.Alert!.Id },
                    result.Alert
                );

            case SosActivationOutcome.Replayed:
                return Ok(result.Alert);

            case SosActivationOutcome.IdempotencyConflict:
                return Conflict(
                    new { message = "La Idempotency-Key ya fue usada con un payload distinto." }
                );

            case SosActivationOutcome.ActiveExists:
                return Conflict(
                    new
                    {
                        message = "El paciente ya tiene una alerta SOS activa.",
                        alertId = result.Alert?.Id,
                    }
                );

            default:
                // Rate-limit distribuido: 429 ANTES de persistir o invocar
                // Twilio/FCM, con Retry-After explícito (REQ-SOS-02).
                Response.Headers["Retry-After"] = result.RetryAfterSeconds.ToString();
                return StatusCode(
                    StatusCodes.Status429TooManyRequests,
                    new { message = "Demasiadas activaciones de SOS. Reintenta más tarde." }
                );
        }
    }

    /// <summary>
    /// Bandeja de alertas SOS para el staff ERP (borrador del front): listado
    /// paginado con filtro opcional por estado. Staff-only (aud=erp + permiso
    /// <c>Sos.Alerts.Manage</c>) con el mismo criterio de scope que la
    /// atención (D5): asignación directa, clínica u organización; los roles
    /// Admin/OrgAdmin/ClinicAdmin ven el listado completo. DTO sin PII
    /// innecesaria (sin teléfono ni coordenadas).
    /// </summary>
    [HttpGet]
    [Authorize(SosPolicies.ErpStaff)]
    [ProducesResponseType(typeof(SosAlertsPage), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SosAlertsPage>> ListAlerts(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    )
    {
        if (!await context.HasPermissionAsync(PermissionCodes.SosAlertsManage, ct))
        {
            return Forbid();
        }

        // El estado del filtro se valida a enum (case-insensitive): un valor
        // ajeno al catálogo responde 400, nunca filtra silenciosamente.
        string? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<SosAlertStatus>(status, ignoreCase: true, out var parsed))
            {
                return BadRequest(new { message = "Estado inválido" });
            }

            parsedStatus = parsed.ToString();
        }

        var staff = await actorContext.ResolveStaffActorAsync(ct);

        // Scope (D5): bypass de administración → sin filtro (null); clínico →
        // pacientes alcanzables por asignación/clínica/organización (lista
        // vacía = denegar: el repositorio no abre el alcance).
        IReadOnlyList<Guid>? patientIds = staff.BypassScope
            ? null
            : await repository.ResolveScopedPatientIdsAsync(
                staff.ProfessionalId,
                staff.ActiveClinicId,
                staff.ActiveOrganizationId,
                ct
            );

        return Ok(
            await mediator.Send(
                new ListSosAlertsForStaffQuery(patientIds, parsedStatus, page, pageSize),
                ct
            )
        );
    }

    /// <summary>Alerta activa del paciente autenticado (sondeo ligero de la app). 204 si no hay.</summary>
    [HttpGet("active")]
    [Authorize(SosPolicies.AppPatient)]
    [ProducesResponseType(typeof(SosAlertDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<SosAlertDto>> GetActive(CancellationToken ct)
    {
        var patientId = await actorContext.ResolvePatientProfileIdAsync(ct);
        if (patientId is null)
        {
            return Forbid();
        }

        var alert = await mediator.Send(new GetActiveSosAlertQuery(patientId.Value), ct);
        return alert is null ? NoContent() : Ok(alert);
    }

    /// <summary>Cancelación por el paciente dueño (transición terminal). IDOR → 404 no revelador.</summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(SosPolicies.AppPatient)]
    [ProducesResponseType(typeof(SosAlertDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SosAlertDto>> Cancel(Guid id, CancellationToken ct)
    {
        var patientId = await actorContext.ResolvePatientProfileIdAsync(ct);
        if (patientId is null)
        {
            return Forbid();
        }

        var result = await mediator.Send(
            new CancelSosAlertCommand(patientId.Value, id, context.UserId ?? Guid.Empty),
            ct
        );
        return MapTransition(result);
    }

    /// <summary>
    /// Atención por staff ERP con permiso <c>Sos.Alerts.Manage</c> y scope
    /// clínico sobre el paciente (D5). Sin scope → 403 no revelador;
    /// alerta ajena → 404; estado terminal → 409.
    /// </summary>
    [HttpPost("{id:guid}/attend")]
    [Authorize(SosPolicies.ErpStaff)]
    [ProducesResponseType(typeof(SosAlertDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SosAlertDto>> Attend(Guid id, CancellationToken ct)
    {
        if (!await context.HasPermissionAsync(PermissionCodes.SosAlertsManage, ct))
        {
            return Forbid();
        }

        var result = await mediator.Send(
            new AttendSosAlertCommand(id, await actorContext.ResolveStaffActorAsync(ct)),
            ct
        );
        return MapTransition(result);
    }

    /// <summary>
    /// Detalle de una alerta: dueño (aud=app) o staff con scope (aud=erp +
    /// <c>Sos.Alerts.Manage</c>). Cruces anti-IDOR → 404/403 no reveladores.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SosAlertDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SosAlertDto>> GetAlert(Guid id, CancellationToken ct)
    {
        if (User.HasClaim("aud", "app"))
        {
            var patientId = await actorContext.ResolvePatientProfileIdAsync(ct);
            if (patientId is null)
            {
                return Forbid();
            }

            var alert = await mediator.Send(new GetSosAlertQuery(patientId.Value, id), ct);
            return alert is null ? NotFound(new { message = "Alerta no encontrada" }) : Ok(alert);
        }

        if (User.HasClaim("aud", "erp"))
        {
            if (!await context.HasPermissionAsync(PermissionCodes.SosAlertsManage, ct))
            {
                return Forbid();
            }

            var staff = await mediator.Send(
                new GetSosAlertForStaffQuery(id, await actorContext.ResolveStaffActorAsync(ct)),
                ct
            );
            return staff.Outcome switch
            {
                SosTransitionOutcome.Transited when staff.Alert is not null => Ok(staff.Alert),
                SosTransitionOutcome.Forbidden => Forbid(),
                _ => NotFound(new { message = "Alerta no encontrada" }),
            };
        }

        // JWT con audiencia no reconocida: 403 (la política de audiencia del
        // endpoint no aplica a esta ruta dual, se resuelve explícitamente).
        return Forbid();
    }

    private ActionResult<SosAlertDto> MapTransition(SosTransitionResult result) =>
        result.Outcome switch
        {
            SosTransitionOutcome.Transited when result.Alert is not null => Ok(result.Alert),
            SosTransitionOutcome.Forbidden => Forbid(),
            SosTransitionOutcome.NotActive => Conflict(
                new { message = "La alerta ya fue atendida o cancelada." }
            ),
            _ => NotFound(new { message = "Alerta no encontrada" }),
        };
}
