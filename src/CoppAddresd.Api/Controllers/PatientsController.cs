using CoppAddresd.Api.Context;
using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class PatientsController(IMediator mediator, ICurrentContext context) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<ActionResult<PatientStatsDto>> Stats(CancellationToken ct)
    {
        var (allowed, ownProfessionalId) = await ResolvePatientScopeAsync(ct);
        if (!allowed)
            return Forbid();

        return Ok(
            await mediator.Send(
                new GetPatientsStatsQuery(context.ActiveClinicId, ownProfessionalId),
                ct
            )
        );
    }

    /// <summary>
    /// Agregados del dashboard general de pacientes (demografía, crecimiento,
    /// top de profesionales y distribución por estado). <c>state</c> acota
    /// demografía/crecimiento/top; el mapa viaja completo.
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<PatientDashboardDto>> Dashboard(
        [FromQuery] string? state = null,
        [FromQuery] int months = 12,
        CancellationToken ct = default
    )
    {
        var (allowed, ownProfessionalId) = await ResolvePatientScopeAsync(ct);
        if (!allowed)
            return Forbid();

        return Ok(
            await mediator.Send(
                new GetPatientsDashboardQuery(
                    context.ActiveClinicId,
                    ownProfessionalId,
                    state,
                    months
                ),
                ct
            )
        );
    }

    /// <summary>
    /// Tablero clínico por paciente (riesgo, alertas, evaluaciones y
    /// seguimiento), paginado y filtrable, con el mismo alcance del listado.
    /// </summary>
    [HttpGet("clinical-board")]
    public async Task<ActionResult<PaginatedClinicalBoardResult>> ClinicalBoard(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? risk = null,
        [FromQuery] bool? hasAlerts = null,
        [FromQuery] string? followUp = null,
        CancellationToken ct = default
    )
    {
        var (allowed, ownProfessionalId) = await ResolvePatientScopeAsync(ct);
        if (!allowed)
            return Forbid();

        return Ok(
            await mediator.Send(
                new GetClinicalBoardQuery(
                    page,
                    pageSize,
                    search,
                    risk,
                    hasAlerts,
                    followUp,
                    context.ActiveClinicId,
                    ownProfessionalId
                ),
                ct
            )
        );
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedPatientsResult>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? insurerId = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null,
        [FromQuery] string? state = null,
        CancellationToken ct = default
    )
    {
        var (allowed, ownProfessionalId) = await ResolvePatientScopeAsync(ct);
        if (!allowed)
            return Forbid();

        // Frontera de datos (Fase 4): con clínica activa solo se ven sus
        // pacientes; sin contexto activo se ve el directorio completo. El
        // alcance "propio" (profesional clínico) restringe además a sus
        // pacientes asignados; el id se resuelve por identidad del JWT.
        var result = await mediator.Send(
            new ListPatientsQuery(
                page,
                pageSize,
                search,
                status,
                insurerId,
                context.ActiveClinicId,
                ownProfessionalId,
                sortBy,
                sortDir,
                state
            ),
            ct
        );
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PatientDto>> GetById(Guid id, CancellationToken ct)
    {
        var (allowed, ownProfessionalId) = await ResolvePatientScopeAsync(ct);
        if (!allowed)
            return Forbid();

        var patient = await mediator.Send(new GetPatientQuery(id), ct);
        if (
            patient is null
            || IsOutsideActiveClinic(patient)
            || await IsOutsideOwnScopeAsync(patient.Id, ownProfessionalId, ct)
        )
            return NotFound(new { message = "Paciente no encontrado" });

        return Ok(patient);
    }

    [HttpPost]
    public async Task<ActionResult<PatientDto>> Create(
        [FromBody] CreatePatientRequest request,
        CancellationToken ct
    )
    {
        if (!await context.HasPermissionAsync("Patients.Create", ct))
            return Forbid();

        var command = new CreatePatientCommand(
            request.MedicalRecordNumber,
            request.FirstName,
            request.MiddleName,
            request.LastName,
            request.DocumentTypeId,
            request.DocumentNumber,
            request.DateOfBirth,
            request.Gender,
            request.EthnicityId,
            request.BloodTypeId,
            request.PhoneCountryCode,
            request.PhoneNumber,
            request.Email,
            request.Address,
            request.CityId,
            request.StateId,
            request.CountryId,
            request.PostalCode,
            request.EmergencyContact,
            request.InsurerId,
            request.MemberId,
            request.MaritalStatus,
            request.SmokingStatus,
            request.AlcoholStatus,
            request.ExerciseLevel,
            request.Disability,
            request.HospitalizationHistory,
            request.SurgeryHistory,
            request.Status,
            request.Notes,
            // El paciente se crea en la clínica activa del contexto; nunca se
            // acepta una clínica del cuerpo (el actor queda en created_by).
            context.ActiveClinicId,
            null,
            context.UserId,
            // Auto-asignación: si el creador es profesional clínico, el
            // paciente queda asignado a él (resuelto del JWT, no del payload).
            await context.GetProfessionalIdAsync(ct),
            request.Diagnoses,
            request.Medications,
            request.Allergies,
            request.VitalSigns
        );

        var patient = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = patient.Id }, patient);
    }

    /// <summary>
    /// Creación masiva de pacientes desde CSV. Cada fila es independiente;
    /// las válidas se crean aunque otras fallen. Cap de 500 filas.
    /// </summary>
    [HttpPost("bulk")]
    public async Task<ActionResult<BulkCreatePatientsResultDto>> Bulk(
        [FromBody] BulkCreatePatientsRequest request,
        CancellationToken ct
    )
    {
        if (!await context.HasPermissionAsync("Patients.Create", ct))
            return Forbid();

        var command = new BulkCreatePatientsCommand(
            request.ClinicId ?? context.ActiveClinicId,
            request.Rows,
            context.UserId,
            await context.GetProfessionalIdAsync(ct)
        );

        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PatientDto>> Update(
        Guid id,
        [FromBody] UpdatePatientRequest request,
        CancellationToken ct
    )
    {
        if (!await context.HasPermissionAsync("Patients.Update", ct))
            return Forbid();

        var (_, ownProfessionalId) = await ResolvePatientScopeAsync(ct);
        if (!await CanAccessPatientAsync(id, ownProfessionalId, ct))
            return NotFound(new { message = "Paciente no encontrado" });

        var command = new UpdatePatientCommand(
            id,
            request.MedicalRecordNumber,
            request.FirstName,
            request.MiddleName,
            request.LastName,
            request.DocumentTypeId,
            request.DocumentNumber,
            request.DateOfBirth,
            request.Gender,
            request.EthnicityId,
            request.BloodTypeId,
            request.PhoneCountryCode,
            request.PhoneNumber,
            request.Email,
            request.Address,
            request.CityId,
            request.StateId,
            request.CountryId,
            request.PostalCode,
            request.EmergencyContact,
            request.InsurerId,
            request.MemberId,
            request.MaritalStatus,
            request.SmokingStatus,
            request.AlcoholStatus,
            request.ExerciseLevel,
            request.Disability,
            request.HospitalizationHistory,
            request.SurgeryHistory,
            request.Status,
            request.Notes,
            context.UserId,
            request.Diagnoses,
            request.Medications,
            request.Allergies,
            request.VitalSigns
        );

        var updated = await mediator.Send(command, ct);
        if (updated is null)
            return NotFound(new { message = "Paciente no encontrado" });

        return Ok(updated);
    }

    /// <summary>
    /// Cambia únicamente el estado operativo del paciente (Activo↔Inactivo)
    /// desde el listado. Endpoint dedicado: no toca diagnósticos, medicamentos,
    /// alergias ni vitales.
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<PatientStatusResultDto>> UpdateStatus(
        Guid id,
        [FromBody] UpdatePatientStatusRequest request,
        CancellationToken ct
    )
    {
        if (!await context.HasPermissionAsync("Patients.Update", ct))
            return Forbid();

        var (_, ownProfessionalId) = await ResolvePatientScopeAsync(ct);
        if (!await CanAccessPatientAsync(id, ownProfessionalId, ct))
            return NotFound(new { message = "Paciente no encontrado" });

        var result = await mediator.Send(
            new UpdatePatientStatusCommand(id, request.Status, context.UserId),
            ct
        );
        if (result is null)
            return NotFound(new { message = "Paciente no encontrado" });

        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!await context.HasPermissionAsync("Patients.Delete", ct))
            return Forbid();

        var (_, ownProfessionalId) = await ResolvePatientScopeAsync(ct);
        if (!await CanAccessPatientAsync(id, ownProfessionalId, ct))
            return NotFound(new { message = "Paciente no encontrado" });

        var deleted = await mediator.Send(new DeletePatientCommand(id, context.UserId), ct);
        if (!deleted)
            return NotFound(new { message = "Paciente no encontrado" });

        return NoContent();
    }

    // --- Asignación paciente ↔ profesional ("mis pacientes") ---

    /// <summary>Profesionales asignados al paciente (detalle: quién lo atiende).</summary>
    [HttpGet("{id:guid}/professionals")]
    public async Task<ActionResult<IReadOnlyList<PatientProfessionalAssignmentView>>> Assignments(
        Guid id,
        CancellationToken ct
    )
    {
        var (allowed, ownProfessionalId) = await ResolvePatientScopeAsync(ct);
        if (!allowed || !await CanAccessPatientAsync(id, ownProfessionalId, ct))
            return NotFound(new { message = "Paciente no encontrado" });

        return Ok(await mediator.Send(new ListPatientAssignmentsQuery(id), ct));
    }

    /// <summary>
    /// Asigna un profesional a un paciente (Patients.Update). Un usuario con
    /// alcance "propio" (profesional clínico) solo puede asignarse a sí mismo:
    /// nunca escalar acceso sobre pacientes de otros profesionales.
    /// </summary>
    [HttpPost("{id:guid}/professionals")]
    public async Task<ActionResult<PatientProfessionalAssignmentView>> Assign(
        Guid id,
        [FromBody] AssignPatientProfessionalRequest request,
        CancellationToken ct
    )
    {
        if (!await context.HasPermissionAsync("Patients.Update", ct))
            return Forbid();

        var (fullScope, ownProfessionalId) = await ResolvePatientScopeAsync(ct);
        if (!fullScope || !await CanAccessPatientAsync(id, ownProfessionalId, ct))
            return NotFound(new { message = "Paciente no encontrado" });

        // Regla de escalada: con alcance propio solo se permite asignarse a sí
        // mismo; la asignación de otros profesionales es administrativa.
        if (ownProfessionalId is not null && request.ProfessionalId != ownProfessionalId.Value)
            return Forbid();

        var assignment = await mediator.Send(
            new AssignPatientProfessionalCommand(
                id,
                request.ProfessionalId,
                context.ActiveClinicId,
                request.RelationshipType,
                context.UserId
            ),
            ct
        );

        return Ok(assignment);
    }

    /// <summary>Desasigna un profesional de un paciente (Patients.Update).</summary>
    [HttpDelete("{id:guid}/professionals/{professionalId:guid}")]
    public async Task<IActionResult> Remove(Guid id, Guid professionalId, CancellationToken ct)
    {
        if (!await context.HasPermissionAsync("Patients.Update", ct))
            return Forbid();

        var (fullScope, ownProfessionalId) = await ResolvePatientScopeAsync(ct);
        if (!fullScope || !await CanAccessPatientAsync(id, ownProfessionalId, ct))
            return NotFound(new { message = "Paciente no encontrado" });

        if (ownProfessionalId is not null && professionalId != ownProfessionalId.Value)
            return Forbid();

        await mediator.Send(new RemovePatientProfessionalCommand(id, professionalId), ct);
        return NoContent();
    }

    /// <summary>
    /// Mediciones clínicas del paciente (ERP, solo lectura): lista plana
    /// ordenada por observación descendente. Alcance por
    /// <see cref="ResolvePatientScopeAsync"/> (403 sin Patients.View/ViewOwn) +
    /// accesibilidad (404 para pacientes desconocidos, de otra clínica o no
    /// asignados). El agrupado por <c>batchId</c> es responsabilidad del
    /// frontend; sin mediciones responde 200 [].
    /// </summary>
    [HttpGet("{id:guid}/measurements")]
    public async Task<ActionResult<IReadOnlyList<PatientMeasurementDto>>> Measurements(
        Guid id,
        CancellationToken ct
    )
    {
        var (allowed, ownProfessionalId) = await ResolvePatientScopeAsync(ct);
        if (!allowed)
            return Forbid();

        if (!await CanAccessPatientAsync(id, ownProfessionalId, ct))
            return NotFound(new { message = "Paciente no encontrado" });

        return Ok(await mediator.Send(new ListPatientMeasurementsQuery(id), ct));
    }

    // --- Helpers de alcance de datos ---

    /// <summary>
    /// Resuelve el alcance de datos del usuario: full (Patients.View) o propio
    /// (Patients.ViewOwn → solo pacientes asignados a su profesional). La
    /// autorización es positiva: sin ninguno de los dos, no hay acceso.
    /// </summary>
    private async Task<(bool Allowed, Guid? OwnProfessionalId)> ResolvePatientScopeAsync(
        CancellationToken ct
    )
    {
        if (await context.HasPermissionAsync("Patients.View", ct))
            return (true, null);

        if (await context.HasPermissionAsync("Patients.ViewOwn", ct))
            return (true, await context.GetProfessionalIdAsync(ct));

        return (false, null);
    }

    /// <summary>
    /// ¿Puede el usuario acceder al paciente? Frontera de clínica (404 para
    /// pacientes de otra clínica) + alcance propio (debe tener asignación
    /// activa). Sin alcance propio, cualquier paciente del scope es accesible.
    /// </summary>
    private async Task<bool> CanAccessPatientAsync(
        Guid patientId,
        Guid? ownProfessionalId,
        CancellationToken ct
    )
    {
        var patient = await mediator.Send(new GetPatientQuery(patientId), ct);
        if (patient is null || IsOutsideActiveClinic(patient))
            return false;

        return !await IsOutsideOwnScopeAsync(patientId, ownProfessionalId, ct);
    }

    /// <summary>
    /// Con alcance propio, el paciente debe tener una asignación activa hacia
    /// el profesional del JWT: nunca se acepta un professionalId del cliente.
    /// </summary>
    private async Task<bool> IsOutsideOwnScopeAsync(
        Guid patientId,
        Guid? ownProfessionalId,
        CancellationToken ct
    )
    {
        if (ownProfessionalId is null)
        {
            return false;
        }

        return !await mediator.Send(
            new PatientIsAssignedQuery(patientId, ownProfessionalId.Value),
            ct
        );
    }

    /// <summary>
    /// Con clínica activa (X-Clinic-Id) un paciente de otra clínica se trata
    /// como inexistente (404): no se filtra por clínica en el detalle y se
    /// evita filtrar existencia entre clínicas. El directorio legacy sin
    /// clínica solo es visible sin contexto activo.
    /// </summary>
    private bool IsOutsideActiveClinic(PatientDto patient) =>
        context.ActiveClinicId is { } clinicId && patient.ClinicId != clinicId;
}
