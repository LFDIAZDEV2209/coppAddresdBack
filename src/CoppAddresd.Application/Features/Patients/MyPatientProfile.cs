using CoppAddresd.Domain.ValueObjects;
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
    string MemberId,
    SecondaryEmergencyContactDto? SecondaryEmergencyContact = null
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
    string? MemberId,
    SecondaryEmergencyContactDto? SecondaryEmergencyContact = null,
    bool ClearInsurer = false
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
        var editsPrimary = request.EmergencyName is not null || request.EmergencyRelationship is not null
            || request.EmergencyPhone is not null || request.EmergencyEmail is not null;
        if (editsPrimary && (string.IsNullOrWhiteSpace(request.EmergencyName) || string.IsNullOrWhiteSpace(request.EmergencyPhone)))
            return new(false, "El contacto principal requiere nombre y teléfono.", null);
        if (editsPrimary && !ValidContact(request.EmergencyName, request.EmergencyRelationship, request.EmergencyPhone, request.EmergencyEmail))
            return new(false, "Revisa los datos del contacto principal.", null);
        var secondary = request.SecondaryEmergencyContact;
        var hasSecondary = secondary is not null && new[] { secondary.Name, secondary.Relationship, secondary.Phone, secondary.Email }.Any(value => !string.IsNullOrWhiteSpace(value));
        if (hasSecondary && (string.IsNullOrWhiteSpace(secondary!.Name) || string.IsNullOrWhiteSpace(secondary.Phone)
            || !ValidContact(secondary.Name, secondary.Relationship, secondary.Phone, secondary.Email)))
            return new(false, "El segundo contacto requiere nombre y teléfono válidos.", null);

        if (hasSecondary) secondary = secondary! with { Phone = EmergencyContactPhone.Normalize(secondary.Phone) };

        var patient = await patients.GetByUserIdAsync(request.UserId, ct);
        if (patient is null)
            return new(false, "No hay un perfil de paciente vinculado a tu cuenta.", null);

        var savedPrimary = EmergencyContactCodec.Deserialize(patient.EmergencyContact);
        if (patient.Status == "Pendiente" && !editsPrimary
            && (savedPrimary is null || string.IsNullOrWhiteSpace(savedPrimary.Name) || string.IsNullOrWhiteSpace(savedPrimary.Phone)
                || !ValidContact(savedPrimary.Name, savedPrimary.Relationship, savedPrimary.Phone, savedPrimary.Email)))
            return new(false, "El contacto principal requiere nombre y teléfono.", null);

        if (request.DateOfBirth.HasValue)
            patient.DateOfBirth = request.DateOfBirth.Value;
        if (!string.IsNullOrWhiteSpace(request.Email))
            patient.Email = request.Email.Trim();
        if (!string.IsNullOrWhiteSpace(request.Phone))
            patient.PhoneNumber = request.Phone.Trim();
        if (request.InsurerId.HasValue || request.ClearInsurer)
            patient.InsurerId = request.InsurerId;
        if (request.MemberId is not null)
            patient.MemberId = request.MemberId.Trim();

        // JSON compatible con SOS: el principal sigue en raíz y el opcional es secondary.
        if (editsPrimary || secondary is not null)
        {
            var previous = EmergencyContactCodec.Deserialize(patient.EmergencyContact);
            var primary = editsPrimary
                ? new EmergencyContactDto(request.EmergencyName, request.EmergencyRelationship, EmergencyContactPhone.Normalize(request.EmergencyPhone), request.EmergencyEmail)
                : previous;
            if (primary is null || string.IsNullOrWhiteSpace(primary.Name) || string.IsNullOrWhiteSpace(primary.Phone)) return new(false, "El contacto principal requiere nombre y teléfono.", null);
            patient.EmergencyContact = EmergencyContactCodec.Serialize(primary with {
                Secondary = secondary is null ? previous?.Secondary : hasSecondary ? secondary : null
            });
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
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
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
    private static bool ValidContact(string? name, string? relationship, string? phone, string? email)
    {
        if (name?.Length > 200 || relationship?.Length > 100 || phone?.Length > 20 || email?.Length > 320) return false;
        if (EmergencyContactPhone.Normalize(phone) is null) return false;
        return string.IsNullOrWhiteSpace(email) || System.Net.Mail.MailAddress.TryCreate(email.Trim(), out _);
    }
}

public static class PatientSelfProfileMapper
{
    public static PatientSelfProfileDto FromEntity(PatientProfile patient)
    {
        var contact = EmergencyContactCodec.Deserialize(patient.EmergencyContact);

        return new PatientSelfProfileDto(
            patient.Id,
            patient.FirstName,
            patient.LastName ?? string.Empty,
            patient.DocumentNumber ?? string.Empty,
            patient.DateOfBirth,
            patient.Email ?? string.Empty,
            patient.PhoneNumber ?? string.Empty,
            contact?.Name ?? string.Empty,
            contact?.Relationship ?? string.Empty,
            contact?.Phone ?? string.Empty,
            contact?.Email ?? string.Empty,
            patient.InsurerId,
            patient.MemberId ?? string.Empty,
            contact?.Secondary
        );
    }
}
