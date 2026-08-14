using CoppAddresd.Application.Features.Patients;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class PatientsController(IMediator mediator) : ControllerBase
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
        var result = await mediator.Send(
            new ListPatientsQuery(page, pageSize, search, status, insurerId), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PatientDto>> GetById(Guid id, CancellationToken ct)
    {
        var patient = await mediator.Send(new GetPatientQuery(id), ct);
        if (patient is null)
            return NotFound(new { message = "Paciente no encontrado" });

        return Ok(patient);
    }

    [HttpPost]
    public async Task<ActionResult<PatientDto>> Create(
        [FromBody] CreatePatientRequest request,
        CancellationToken ct)
    {
        var command = new CreatePatientCommand(
            request.MedicalRecordNumber,
            request.FirstName,
            request.MiddleName,
            request.LastName,
            request.DocumentType,
            request.DocumentNumber,
            request.DateOfBirth,
            request.Gender,
            request.Ethnicity,
            request.BloodType,
            request.Phone,
            request.Email,
            request.Address,
            request.City,
            request.State,
            request.PostalCode,
            request.EmergencyContact,
            request.InsurerId,
            request.MemberId,
            request.SmokingStatus,
            request.AlcoholStatus,
            request.ExerciseLevel,
            request.Disability,
            request.HospitalizationHistory,
            request.SurgeryHistory,
            request.Status,
            request.Notes,
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
        var command = new UpdatePatientCommand(
            id,
            request.MedicalRecordNumber,
            request.FirstName,
            request.MiddleName,
            request.LastName,
            request.DocumentType,
            request.DocumentNumber,
            request.DateOfBirth,
            request.Gender,
            request.Ethnicity,
            request.BloodType,
            request.Phone,
            request.Email,
            request.Address,
            request.City,
            request.State,
            request.PostalCode,
            request.EmergencyContact,
            request.InsurerId,
            request.MemberId,
            request.SmokingStatus,
            request.AlcoholStatus,
            request.ExerciseLevel,
            request.Disability,
            request.HospitalizationHistory,
            request.SurgeryHistory,
            request.Status,
            request.Notes,
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
        var deleted = await mediator.Send(new DeletePatientCommand(id), ct);
        if (!deleted)
            return NotFound(new { message = "Paciente no encontrado" });

        return NoContent();
    }
}