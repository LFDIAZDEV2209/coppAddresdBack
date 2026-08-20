using CoppAddresd.Api.Context;
using CoppAddresd.Application.Features.Patients;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class PatientsController(IMediator mediator, ICurrentContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedPatientsResult>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? insurerId = null,
        CancellationToken ct = default)
    {
        if (!await context.HasPermissionAsync("Patients.View", ct))
            return Forbid();

        // Frontera de datos (Fase 4): con clínica activa solo se ven sus
        // pacientes; sin contexto activo se ve el directorio completo.
        var result = await mediator.Send(
            new ListPatientsQuery(page, pageSize, search, status, insurerId, context.ActiveClinicId), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PatientDto>> GetById(Guid id, CancellationToken ct)
    {
        if (!await context.HasPermissionAsync("Patients.View", ct))
            return Forbid();

        var patient = await mediator.Send(new GetPatientQuery(id), ct);
        if (patient is null || IsOutsideActiveClinic(patient))
            return NotFound(new { message = "Paciente no encontrado" });

        return Ok(patient);
    }

    [HttpPost]
    public async Task<ActionResult<PatientDto>> Create(
        [FromBody] CreatePatientRequest request,
        CancellationToken ct)
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
            request.Diagnoses,
            request.Medications,
            request.Allergies,
            request.VitalSigns);

        var patient = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = patient.Id }, patient);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PatientDto>> Update(
        Guid id,
        [FromBody] UpdatePatientRequest request,
        CancellationToken ct)
    {
        if (!await context.HasPermissionAsync("Patients.Update", ct))
            return Forbid();

        var current = await mediator.Send(new GetPatientQuery(id), ct);
        if (current is null || IsOutsideActiveClinic(current))
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
            request.VitalSigns);

        var updated = await mediator.Send(command, ct);
        if (updated is null)
            return NotFound(new { message = "Paciente no encontrado" });

        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!await context.HasPermissionAsync("Patients.Delete", ct))
            return Forbid();

        var current = await mediator.Send(new GetPatientQuery(id), ct);
        if (current is null || IsOutsideActiveClinic(current))
            return NotFound(new { message = "Paciente no encontrado" });

        var deleted = await mediator.Send(new DeletePatientCommand(id, context.UserId), ct);
        if (!deleted)
            return NotFound(new { message = "Paciente no encontrado" });

        return NoContent();
    }

    /// <summary>
    /// Con clínica activa (X-Clinic-Id) un paciente de otra clínica se trata
    /// como inexistente (404): no se filtra por clínica en el detalle y se
    /// evita filtrar existencia entre clínicas. El directorio legacy sin
    /// clínica solo es visible sin contexto activo.
    /// </summary>
    private bool IsOutsideActiveClinic(PatientDto patient)
        => context.ActiveClinicId is { } clinicId && patient.ClinicId != clinicId;
}