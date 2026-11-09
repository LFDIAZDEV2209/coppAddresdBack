using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>Fan-out durable por contacto, sin duplicar un mismo teléfono/correo.</summary>
internal static class SosRecipientDispatch
{
    public static string Key(string channel, Guid alertId, SosAlertRecipient? recipient) =>
        $"sos:{channel}:{alertId}" + (recipient is { ContactOrder: > 0 } ? $":{recipient.ContactOrder}" : "");

    public static async Task<SosChannelStatus> RunAsync(SosAlert alert, string channel,
        INotificationDedupeRepository dedupe, AppDbContext? db,
        Func<SosAlert, SosAlertRecipient?, CancellationToken, Task<SosChannelStatus>> send,
        ILogger logger, CancellationToken ct)
    {
        if (alert.Recipients.Count == 0) return await send(alert, null, ct);
        var processed = new Dictionary<string, (SosAlertRecipient Recipient, SosAlert Delivery, SosChannelStatus Status)>(StringComparer.OrdinalIgnoreCase);
        var statuses = new List<SosChannelStatus>();
        foreach (var recipient in alert.Recipients.OrderBy(r => r.ContactOrder))
        {
            ct.ThrowIfCancellationRequested();
            var destination = channel == "email" ? recipient.Email : recipient.PhoneE164;
            var delivery = Copy(alert, recipient);
            SosChannelStatus status;
            if (string.IsNullOrWhiteSpace(destination)) status = SosChannelStatus.NoConfigurado;
            else if (processed.TryGetValue(destination, out var prior))
            {
                status = prior.Status;
                delivery = Copy(prior.Delivery, recipient);
                recipient.SmsProviderMessageId = prior.Recipient.SmsProviderMessageId;
                delivery.VoiceProviderCallId = prior.Delivery.VoiceProviderCallId;
            }
            else
            {
                try { status = await send(delivery, recipient, ct); }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    status = SosChannelStatus.Fallido;
                    logger.LogWarning("SOS contacto no procesado: alertId={AlertId}, slot={Slot}, channel={Channel}, exceptionType={Type}.",
                        alert.Id, recipient.ContactOrder, channel, ex.GetType().Name);
                }
                processed[destination] = (recipient, delivery, status);
            }
            // También finaliza el outbox de destinos duplicados o sin destino.
            await dedupe.UpsertAsync(Key(channel, alert.Id, recipient), Guid.Empty,
                pushStatus: null, smsStatus: channel == "sms" ? status.ToString().ToLowerInvariant() : null,
                voiceStatus: channel == "voice" ? status.ToString().ToLowerInvariant() : null,
                emailStatus: channel == "email" ? status.ToString().ToLowerInvariant() : null, ct: ct);
            if (db is not null) await StoreAsync(db, delivery, recipient, channel, status, ct);
            statuses.Add(status);
        }
        return statuses.All(s => s == statuses[0]) ? statuses[0] : SosChannelStatus.Fallido;
    }

    private static SosAlert Copy(SosAlert alert, SosAlertRecipient recipient) => new()
    {
        Id = alert.Id, PatientId = alert.PatientId, Patient = alert.Patient, CreatedAt = alert.CreatedAt,
        DestinationPhoneE164 = recipient.PhoneE164 ?? "", Latitude = alert.Latitude, Longitude = alert.Longitude,
        AccuracyMeters = alert.AccuracyMeters, LocationCapturedAt = alert.LocationCapturedAt,
        LocationDescription = alert.LocationDescription, LocationLookupCompletedAt = alert.LocationLookupCompletedAt,
        LocationLinkHash = alert.LocationLinkHash,
        HeartRate = alert.HeartRate, Spo2 = alert.Spo2, BloodPressure = alert.BloodPressure,
        SmsChannelStatus = recipient.SmsChannelStatus, SmsDeliveryStatus = recipient.SmsDeliveryStatus,
        SmsDetail = recipient.SmsDetail, VoiceChannelStatus = recipient.VoiceChannelStatus,
        VoiceDetail = recipient.VoiceDetail, VoiceProviderCallId = recipient.VoiceProviderCallId,
        VoiceCallStatus = recipient.VoiceCallStatus, VoiceAnsweredBy = recipient.VoiceAnsweredBy,
        VoiceDurationSeconds = recipient.VoiceDurationSeconds,
    };

    private static async Task StoreAsync(AppDbContext db, SosAlert delivery, SosAlertRecipient snapshot,
        string channel, SosChannelStatus status, CancellationToken ct)
    {
        var target = await db.SosAlertRecipients.FirstAsync(r => r.Id == snapshot.Id && r.AlertId == delivery.Id, ct);
        var primary = snapshot.ContactOrder == 0
            ? await db.SosAlerts.IgnoreAutoIncludes().FirstAsync(a => a.Id == delivery.Id, ct) : null;
        if (channel == "sms")
        {
            snapshot.SmsChannelStatus = target.SmsChannelStatus = status;
            target.SmsDetail = delivery.SmsDetail; target.SmsProviderMessageId = snapshot.SmsProviderMessageId;
            target.SmsUpdatedAt = DateTime.UtcNow;
            if (primary is not null) { primary.SmsChannelStatus = status; primary.SmsDetail = delivery.SmsDetail; primary.SmsUpdatedAt = target.SmsUpdatedAt; }
        }
        else if (channel == "voice")
        {
            snapshot.VoiceChannelStatus = target.VoiceChannelStatus = status;
            target.VoiceDetail = delivery.VoiceDetail; snapshot.VoiceProviderCallId = target.VoiceProviderCallId = delivery.VoiceProviderCallId;
            target.VoiceUpdatedAt = DateTime.UtcNow;
            if (primary is not null) { primary.VoiceChannelStatus = status; primary.VoiceDetail = delivery.VoiceDetail; primary.VoiceProviderCallId = delivery.VoiceProviderCallId; primary.VoiceUpdatedAt = target.VoiceUpdatedAt; }
        }
        else
        {
            snapshot.EmailChannelStatus = target.EmailChannelStatus = status.ToString();
            target.EmailUpdatedAt = DateTime.UtcNow;
            if (primary is not null) { primary.EmailChannelStatus = status; primary.EmailUpdatedAt = target.EmailUpdatedAt; }
        }
        await db.SaveChangesAsync(ct);
    }
}
