using System.Text.Json;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Patients;

/// <summary>
/// Perfil autogestionado del PACIENTE (APP mÃƒÂ³vil, aud=app). Resuelto por JWT
/// (anti-IDOR: nunca acepta ids en la ruta/cuerpo). Dominio separado del
/// perfil profesional (/me/profile Ã¢â€ â€™ EmployeeDto).
/// </summary>
public record PatientSelfProfileDto(
    Guid PatientId,
    string FirstName,
    string LastName,
    string DocumentNumber,
    DateTime? DateOfBirth,
    string Email,
    string Phone,
    string EmergencyName,
    string EmergencyRelationship,
    string EmergencyPhone,
    string EmergencyEmail,
    Guid? InsurerId,
    string MemberId
);

public record GetMyPatientProfileQuery(Guid UserId) : IRequest<PatientSelfProfileDto?>;

public sealed class GetMyPatientProfileQueryHandler(IPatientRepository patients)
    : IRequestHandler<GetMyPatientProfileQuery, PatientSelfProfileDto?>
{
    public async Task<PatientSelfProfileDto?> Handle(
        GetMyPatientProfileQuery request,
        CancellationToken ct
    )
    {
        var patient = await patients.GetByUserIdAsync(request.UserId, ct);
        return patient is null ? null : PatientSelfProfileMapper.FromEntity(patient);
    }
}

/// <summary>
/// EdiciÃƒÂ³n self-service: solo campos permitidos (dob, email, celular, contacto
/// de emergencia, seguro/pÃƒÂ³liza). Nombre/documento/ÃƒÂ³rdenes de ERP no se tocan.
/// </summary>
public record UpdateMyPatientProfileCommand(
    Guid UserId,
    DateTime? DateOfBirth,
    string? Email,
    string? Phone,
    string? EmergencyName,
    string? EmergencyRelationship,
    string? EmergencyPhone,
    string? EmergencyEmail,
    Guid? InsurerId,
    string? MemberId
) : IRequest<UpdateMyPatientProfileResult>;

public record UpdateMyPatientProfileResult(
    bool Success,
    string? Error,
    PatientSelfProfileDto? Profile
);

public sealed class UpdateMyPatientProfileCommandHandler(
    IPatientRepository patients,
    ILogger<UpdateMyPatientProfileCommandHandler> logger
) : IRequestHandler<UpdateMyPatientProfileCommand, UpdateMyPatientProfileResult>
{
    public async Task<UpdateMyPatientProfileResult> Handle(
        UpdateMyPatientProfileCommand request,
        CancellationToken ct
    )
    {
        // ValidaciÃƒÂ³n de frontera (el ERP mantiene la autoridad sobre identidad;
        // aquÃƒÂ­ solo se editan datos de contacto y clÃƒÂ­nicos bÃƒÂ¡sicos).
        if (request.DateOfBirth is { } dob && dob > DateTime.UtcNow)
            return new(false, "La fecha de nacimiento no puede ser futura.", null);
        if (!string.IsNullOrWhiteSpace(request.Email) && !request.Email.Contains('@'))
            return new(false, "Correo electrÃƒÂ³nico invÃƒÂ¡lido.", null);
        if (
            request.EmergencyName is { Length: > 0 }
            && string.IsNullOrWhiteSpace(request.EmergencyPhone)
        )
            return new(false, "El contacto de emergencia requiere telÃƒÂ©fono.", null);

        var patient = await patients.GetByUserIdAsync(request.UserId, ct);
        if (patient is null)
            return new(false, "No hay un perfil de paciente vinculado a tu cuenta.", null);

        if (request.DateOfBirth.HasValue)
            patient.DateOfBirth = request.DateOfBirth.Value;
        if (!string.IsNullOrWhiteSpace(request.Email))
            patient.Email = request.Email.Trim();
        if (!string.IsNullOrWhiteSpace(request.Phone))
            patient.PhoneNumber = request.Phone.Trim();
        if (request.InsurerId.HasValue)
            patient.InsurerId = request.InsurerId.Value;
        if (request.MemberId is not null)
            patient.MemberId = request.MemberId.Trim();

        // Contacto de emergencia: JSON en emergency_contact (varchar). Al menos
        // nombre o telÃƒÂ©fono; null explÃƒÂ­cito borra el contacto.
        if (
            request.EmergencyName is not null
            || request.EmergencyRelationship is not null
            || request.EmergencyPhone is not null
            || request.EmergencyEmail is not null
        )
        {
            var hasContent =
                !string.IsNullOrWhiteSpace(request.EmergencyName)
                || !string.IsNullOrWhiteSpace(request.EmergencyPhone);
            patient.EmergencyContact = hasContent
                ? JsonSerializer.Serialize(
                    new
                    {
                        name = request.EmergencyName?.Trim(),
                        relationship = request.EmergencyRelationship?.Trim(),
                        phone = request.EmergencyPhone?.Trim(),
                        email = request.EmergencyEmail?.Trim(),
                    }
                )
                : null;
        }

        try
        {
            await patients.UpdateAsync(patient, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "ActualizaciÃƒÂ³n de perfil de paciente {PatientId} fallÃƒÂ³",
                patient.Id
            );
            return new(false, "No se pudo actualizar el perfil (datos invÃƒÂ¡lidos).", null);
        }

        logger.LogInformation(
            "Perfil de paciente {PatientId} actualizado por self-service",
            patient.Id
        );
        return new(true, null, PatientSelfProfileMapper.FromEntity(patient));
    }
}

public static class PatientSelfProfileMapper
{
    public static PatientSelfProfileDto FromEntity(PatientProfile patient)
    {
        string? name = null,
            rel = null,
            phone = null,
            email = null;
        if (!string.IsNullOrWhiteSpace(patient.EmergencyContact))
        {
            try
            {
                var ec = JsonSerializer.Deserialize<JsonElement>(patient.EmergencyContact);
                name =
                    ec.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
                        ? n.GetString()
                        : null;
                rel =
                    ec.TryGetProperty("relationship", out var r)
                    && r.ValueKind == JsonValueKind.String
                        ? r.GetString()
                        : null;
                phone =
                    ec.TryGetProperty("phone", out var p) && p.ValueKind == JsonValueKind.String
                        ? p.GetString()
                        : null;
                email =
                    ec.TryGetProperty("email", out var e) && e.ValueKind == JsonValueKind.String
                        ? e.GetString()
                        : null;
            }
            catch (JsonException)
            {
                // Contacto legacy con formato libre: se expone como texto plano en name.
                name = patient.EmergencyContact;
            }
        }

        return new PatientSelfProfileDto(
            patient.Id,
            patient.FirstName,
            patient.LastName ?? string.Empty,
            patient.DocumentNumber ?? string.Empty,
            patient.DateOfBirth,
            patient.Email ?? string.Empty,
            patient.PhoneNumber ?? string.Empty,
            name ?? string.Empty,
            rel ?? string.Empty,
            phone ?? string.Empty,
            email ?? string.Empty,
            patient.InsurerId,
            patient.MemberId ?? string.Empty
        );
    }
}
