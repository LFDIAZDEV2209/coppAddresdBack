using System.Text.Json;
using CoppAddresd.Application.DTOs.Email;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Application.Features.Sos;

// ============================================================================
// Request / Response DTOs (contrato del frontend)
// ============================================================================

/// <summary>Payload de activación SOS desde la app móvil.</summary>
public sealed record ActivateSosAlertRequest(
    double? Latitude,
    double? Longitude,
    double? AccuracyMeters,
    string? LocationLabel,
    SosVitals? Vitals,
    SosEmergencyContact? EmergencyContact,
    string Language);

public sealed record SosVitals(int? HeartRate, int? Spo2, string? BloodPressure);

public sealed record SosEmergencyContact(
    string Name,
    string Relationship,
    string Phone,
    string Email);

/// <summary>Resultado de la activación SOS.</summary>
public sealed record SosAlertResult(
    Guid Id,
    string Status,
    DateTime TriggeredAt,
    string MessageText,
    string EmergencyNumber,
    SosChannelResult Sms,
    SosChannelResult Email,
    SosChannelResult Voice);

public sealed record SosChannelResult(string Status, string? Detail);

// ============================================================================
// MediatR Command
// ============================================================================

public sealed record ActivateSosAlertCommand(
    Guid PatientId,
    ActivateSosAlertRequest Request) : IRequest<SosAlertResult?>;

// ============================================================================
// Handler
// ============================================================================

public sealed class ActivateSosAlertCommandHandler(
    IPatientRepository patientRepository,
    ISosAlertRepository sosAlertRepository,
    ISmsSender smsSender,
    IVoiceCaller voiceCaller,
    IEmailService emailService,
    IOptions<SosOptions> sosOptions,
    ILogger<ActivateSosAlertCommandHandler> logger) : IRequestHandler<ActivateSosAlertCommand, SosAlertResult?>
{
    public async Task<SosAlertResult?> Handle(ActivateSosAlertCommand request, CancellationToken ct)
    {
        var patient = await patientRepository.GetByIdAsync(request.PatientId, ct);
        if (patient is null)
        {
            return null;
        }

        var req = request.Request;
        var lang = req.Language ?? "es";
        var emergencyNumber = sosOptions.Value.EmergencyNumber;

        // ── Construir bloque de datos de emergencia ──
        var messageText = BuildEmergencyMessage(patient, req, lang, emergencyNumber);

        // ── Vitals snapshot (jsonb) ──
        var vitalsJson = req.Vitals is { } v
            ? JsonSerializer.Serialize(new { v.HeartRate, v.Spo2, v.BloodPressure })
            : null;

        // ── Crear entidad (persistencia inicial) ──
        var alert = new SosAlert
        {
            Id = Guid.NewGuid(),
            PatientId = request.PatientId,
            TriggeredAtUtc = DateTime.UtcNow,
            Latitude = req.Latitude,
            Longitude = req.Longitude,
            AccuracyMeters = req.AccuracyMeters,
            LocationLabel = req.LocationLabel,
            VitalsSnapshot = vitalsJson,
            MessageText = messageText,
            EmergencyContactName = req.EmergencyContact?.Name,
            EmergencyContactRelationship = req.EmergencyContact?.Relationship,
            EmergencyContactPhone = req.EmergencyContact?.Phone,
            EmergencyContactEmail = req.EmergencyContact?.Email,
            Status = "Pending",
            ChannelResults = "{}",
            CreatedAtUtc = DateTime.UtcNow
        };

        await sosAlertRepository.AddAsync(alert, ct);

        // ── Short TTS script (voice channel) ──
        var voiceScript = BuildVoiceScript(patient, req, emergencyNumber);

        // ── Dispatch por canal (best-effort) ──
        var smsResult = await DispatchSmsAsync(req.EmergencyContact, messageText, ct);
        var emailResult = await DispatchEmailAsync(req.EmergencyContact, patient, messageText, ct);
        var voiceResult = await DispatchVoiceAsync(req.EmergencyContact, voiceScript, ct);

        // ── Estado consolidado ──
        var statuses = new[] { smsResult.Status, emailResult.Status, voiceResult.Status };
        var anySent = statuses.Any(s => s == "Sent");
        var anyFailed = statuses.Any(s => s == "Failed");
        var finalStatus = (anySent, anyFailed) switch
        {
            (true, false) => "Sent",
            (true, true) => "Partial",
            (false, true) => "Failed",
            _ => "Disabled"
        };

        // ── Actualizar alerta con resultados ──
        alert.Status = finalStatus;
        alert.ChannelResults = JsonSerializer.Serialize(new
        {
            sms = new { smsResult.Status, Detail = smsResult.Detail },
            email = new { emailResult.Status, Detail = emailResult.Detail },
            voice = new { voiceResult.Status, Detail = voiceResult.Detail }
        });

        await sosAlertRepository.UpdateAsync(alert, ct);

        logger.LogInformation(
            "SOS alerta {AlertId} para paciente {PatientId}: status={Status}, " +
            "sms={SmsStatus}, email={EmailStatus}, voice={VoiceStatus}",
            alert.Id, request.PatientId, finalStatus,
            smsResult.Status, emailResult.Status, voiceResult.Status);

        return new SosAlertResult(
            Id: alert.Id,
            Status: finalStatus,
            TriggeredAt: alert.TriggeredAtUtc,
            MessageText: messageText,
            EmergencyNumber: emergencyNumber,
            Sms: smsResult,
            Email: emailResult,
            Voice: voiceResult);
    }

    // ========================================================================
    // Construcción del mensaje de emergencia (English-only)
    // ========================================================================

    private static string BuildEmergencyMessage(
        PatientProfile patient,
        ActivateSosAlertRequest req,
        string _lang,
        string emergencyNumber)
    {
        var lines = new List<string>();

        lines.Add("=== SOS ALERT - EMERGENCY ===");
        lines.Add("");

        var fullName = BuildFullName(patient);
        lines.Add($"Patient: {fullName}");

        if (patient.DateOfBirth is { } dob)
        {
            var age = CalculateAge(dob);
            lines.Add($"Age: {age} years (DOB: {dob:MM/dd/yyyy})");
        }

        if (!string.IsNullOrWhiteSpace(patient.DocumentNumber))
        {
            lines.Add($"Document: {patient.DocumentNumber}");
        }

        if (!string.IsNullOrWhiteSpace(patient.BloodType?.Code))
        {
            lines.Add($"Blood type: {patient.BloodType.Code}");
        }

        if (!string.IsNullOrWhiteSpace(patient.Insurer?.Name))
        {
            lines.Add($"Insurer: {patient.Insurer.Name}");
        }

        if (!string.IsNullOrWhiteSpace(patient.MemberId))
            lines.Add($"Member ID: {patient.MemberId}");

        // Vital Signs
        if (req.Vitals is { } vitals)
        {
            lines.Add("");
            lines.Add("--- Vital Signs ---");
            if (vitals.HeartRate is { } hr)
                lines.Add($"Heart Rate: {hr} bpm");
            if (vitals.Spo2 is { } spo2)
                lines.Add($"SpO2: {spo2}%");
            if (!string.IsNullOrWhiteSpace(vitals.BloodPressure))
                lines.Add($"Blood Pressure: {vitals.BloodPressure}");
        }

        // Location (no maps URL — address + decimal + DMS)
        lines.Add("");
        var hasAddress = !string.IsNullOrWhiteSpace(req.LocationLabel);
        var hasCoords = req.Latitude is not null && req.Longitude is not null;

        if (hasAddress || hasCoords)
        {
            if (hasAddress)
                lines.Add($"Address: {req.LocationLabel}");

            if (hasCoords)
            {
                var lat = req.Latitude!.Value;
                var lng = req.Longitude!.Value;
                lines.Add($"Decimal: {lat:F6}, {lng:F6}" +
                          (req.AccuracyMeters is { } acc ? $" (±{acc:F0} m)" : ""));
                lines.Add($"DMS: {ToDms(lat, isLat: true)}, {ToDms(lng, isLat: false)}");
            }
        }
        else
        {
            lines.Add("Location: not available");
        }

        // Emergency Contact
        if (req.EmergencyContact is { } contact)
        {
            lines.Add("");
            lines.Add("--- Emergency Contact ---");
            lines.Add($"{contact.Name} ({contact.Relationship})");
            lines.Add($"Phone: {contact.Phone}");
            if (!string.IsNullOrWhiteSpace(contact.Email))
                lines.Add($"Email: {contact.Email}");
        }

        lines.Add("");
        lines.Add($"Call {emergencyNumber} if needed.");

        return string.Join(Environment.NewLine, lines);
    }

    // ========================================================================
    // Short voice-call script (spoken TTS — no URLs, ~60 seconds)
    // ========================================================================

    private static string BuildVoiceScript(
        PatientProfile patient,
        ActivateSosAlertRequest req,
        string emergencyNumber)
    {
        var parts = new List<string>();

        // Opening
        parts.Add("This is an automated SOS emergency alert.");

        // Patient identification
        var fullName = BuildFullName(patient);
        var age = patient.DateOfBirth is { } dob ? CalculateAge(dob) : (int?)null;
        parts.Add(age is { } a
            ? $"The patient is {fullName}, {a} years old."
            : $"The patient is {fullName}.");

        // Vital signs (spoken form)
        if (req.Vitals is { } vitals)
        {
            var vitalsParts = new List<string>();
            if (vitals.HeartRate is { } hr)
                vitalsParts.Add($"heart rate {hr}");
            if (!string.IsNullOrWhiteSpace(vitals.BloodPressure))
                vitalsParts.Add($"blood pressure {vitals.BloodPressure}");
            if (vitals.Spo2 is { } spo2)
                vitalsParts.Add($"oxygen {spo2} percent");
            if (vitalsParts.Count > 0)
                parts.Add($"Vital signs: {string.Join(", ", vitalsParts)}.");
        }

        // Location (spoken decimal + accuracy; text message reference)
        if (req.Latitude is { } lat && req.Longitude is { } lng)
        {
            var locationWords = $"location {lat:F6}, {lng:F6}";
            if (req.AccuracyMeters is { } acc)
                locationWords += $", accuracy plus or minus {acc:F0} meters";
            parts.Add($"The {locationWords} has been sent by text message along with full medical information.");
        }

        // Emergency contact
        if (req.EmergencyContact is { } contact)
        {
            parts.Add($"Emergency contact: {contact.Name}, {contact.Relationship}, phone {contact.Phone}.");
        }

        // Closing
        parts.Add($"Call {emergencyNumber} if needed.");

        return string.Join(" ", parts);
    }

    private static string BuildFullName(PatientProfile patient)
    {
        var parts = new List<string> { patient.FirstName };
        if (!string.IsNullOrWhiteSpace(patient.MiddleName))
            parts.Add(patient.MiddleName);
        parts.Add(patient.LastName);
        return string.Join(" ", parts);
    }

    private static int CalculateAge(DateTime dob)
    {
        var today = DateTime.UtcNow;
        var age = today.Year - dob.Year;
        if (dob.Date > today.AddYears(-age))
            age--;
        return age;
    }

    /// <summary>Convert decimal degrees to DMS string (e.g. "4°42'39.6\" N").</summary>
    private static string ToDms(double decimalDegrees, bool isLat)
    {
        var absolute = Math.Abs(decimalDegrees);
        var degrees = (int)absolute;
        var minutesFull = (absolute - degrees) * 60;
        var minutes = (int)minutesFull;
        var seconds = (minutesFull - minutes) * 60;

        var direction = isLat
            ? (decimalDegrees >= 0 ? "N" : "S")
            : (decimalDegrees >= 0 ? "E" : "W");

        return $"{degrees}°{minutes}'{seconds:F1}\" {direction}";
    }

    // ========================================================================
    // Dispatch por canal
    // ========================================================================

    private async Task<SosChannelResult> DispatchSmsAsync(
        SosEmergencyContact? contact, string messageText, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(contact?.Phone))
            return new SosChannelResult("Skipped", "Sin telefono de contacto");

        try
        {
            await smsSender.SendAsync(new SmsMessage(contact!.Phone, messageText), ct);
            return new SosChannelResult("Sent", null);
        }
        catch (InvalidOperationException)
        {
            // Proveedor deshabilitado (Log provider)
            return new SosChannelResult("Disabled", "Proveedor SMS deshabilitado");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al enviar SMS SOS a {Phone}.", contact!.Phone);
            return new SosChannelResult("Failed", ex.Message);
        }
    }

    private async Task<SosChannelResult> DispatchEmailAsync(
        SosEmergencyContact? contact, PatientProfile patient, string messageText, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(contact?.Email))
            return new SosChannelResult("Skipped", "Sin email de contacto");

        var subject = $"SOS ALERT - {patient.FirstName} {patient.LastName}";

        try
        {
            await emailService.SendEmailAsync(
                contact!.Email,
                subject,
                messageText,
                isHtml: false,
                cancellationToken: ct);
            return new SosChannelResult("Sent", null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al enviar email SOS a {Email}.", contact!.Email);
            return new SosChannelResult("Failed", ex.Message);
        }
    }

    private async Task<SosChannelResult> DispatchVoiceAsync(
        SosEmergencyContact? contact, string voiceScript, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(contact?.Phone))
            return new SosChannelResult("Skipped", "Sin telefono de contacto");

        try
        {
            await voiceCaller.CallAsync(contact!.Phone, voiceScript, "en-US", ct);
            return new SosChannelResult("Sent", null);
        }
        catch (InvalidOperationException)
        {
            return new SosChannelResult("Disabled", "Proveedor de voz deshabilitado");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al enviar llamada de voz SOS a {Phone}.", contact!.Phone);
            return new SosChannelResult("Failed", ex.Message);
        }
    }
}
