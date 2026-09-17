using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Application.Features.HealthTests.Notifications;

/// <summary>
/// Lógica compartida entre el envío real y la vista previa: qué destinatario
/// corresponde a un canal y si el paciente es alcanzable por él.
/// </summary>
internal static class NotificationPreviewSupport
{
    public static (bool IsReachable, string Value, string? SkipReason) ResolveRecipient(
        NotificationChannel channel,
        PatientProfile? patient
    )
    {
        if (patient is null)
        {
            return (false, string.Empty, "La alerta no tiene paciente asociado.");
        }

        if (channel == NotificationChannel.sms)
        {
            if (string.IsNullOrWhiteSpace(patient.PhoneNumber))
            {
                return (false, string.Empty, "El paciente no tiene teléfono registrado.");
            }

            var dial = string.IsNullOrWhiteSpace(patient.PhoneCountryCode)
                ? string.Empty
                : $"+{patient.PhoneCountryCode!.TrimStart('+')}";
            return (true, $"{dial}{patient.PhoneNumber}", null);
        }

        // community
        if (patient.UserId is null || patient.UserId == Guid.Empty)
        {
            return (false, string.Empty, "El paciente no tiene cuenta en la app.");
        }

        return (true, patient.UserId.Value.ToString(), null);
    }
}
