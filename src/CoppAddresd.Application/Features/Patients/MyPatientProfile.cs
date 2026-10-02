using System.Text.Json;
using CoppAddresd.Application.Features.Patients.Events;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Patients;

/// <summary>
/// Perfil autogestionado del PACIENTE (APP móvil, aud=app). Resuelto por JWT
/// (anti-IDOR: nunca acepta ids en la ruta/cuerpo). Dominio separado del
/// perfil profesional (/me/profile → EmployeeDto).
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
/// Edición self-service: solo campos permitidos (dob, email, celular, contacto
/// de emergencia, seguro/póliza). Nombre/documento/órdenes de ERP no se tocan.
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
    ILogger<UpdateMyPatientProfileCommandHandler> logger,
    IPatientMetricsQueue? metricsQueue = null
) : IRequestHandler<UpdateMyPatientProfileCommand, UpdateMyPatientProfileResult>
{
    public async Task<UpdateMyPatientProfileResult> Handle(
        UpdateMyPatientProfileCommand request,
        CancellationToken ct
    )
    {
        // Validación de frontera (el ERP mantiene la autoridad sobre identidad;
        // aquí solo se editan datos de contacto y clínicos básicos).
        if (request.DateOfBirth is { } dob && dob > DateTime.UtcNow)
            return new(false, "La fecha de nacimiento no puede ser futura.", null);
        if (!string.IsNullOrWhiteSpace(request.Email) && !request.Email.Contains('@'))
            return new(false, "Correo electrónico inválido.", null);
        if (
            request.EmergencyName is { Length: > 0 }
            && string.IsNullOrWhiteSpace(request.EmergencyPhone)
        )
            return new(false, "El contacto de emergencia requiere teléfono.", null);

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
        // nombre o teléfono; null explícito borra el contacto.
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

        // Onboarding self-service completado: un registro provisional del ERP
        // (Pendiente) se promueve a Activo al guardar el perfil en la app.
        var previousStatus = patient.Status;
        var promotedFromPending = string.Equals(
            previousStatus,
            "Pendiente",
            StringComparison.Ordinal
        );
        if (promotedFromPending)
            patient.Status = "Activo";

        try
        {
            await patients.UpdateAsync(patient, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Actualización de perfil de paciente {PatientId} falló",
                patient.Id
            );
            return new(false, "No se pudo actualizar el perfil (datos inválidos).", null);
        }

        // Métrica de cambio de estado (misma señal que el toggle del ERP):
        // ajusta los contadores status_count por clínica.
        if (promotedFromPending && metricsQueue is not null)
        {
            await metricsQueue.EnqueueAsync(
                new PatientStatusChangedMetricEvent(
                    patient.Id,
                    patient.ClinicId,
                    previousStatus,
                    "Activo",
                    DateTime.UtcNow
                )
            );
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
