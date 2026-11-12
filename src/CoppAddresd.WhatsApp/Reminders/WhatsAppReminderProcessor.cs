using System.Globalization;
using CoppAddresd.Application.Features.ProgramProgress.Queries;
using CoppAddresd.Application.WhatsApp.Messaging;
using CoppAddresd.Domain.Entities.WhatsApp;
using CoppAddresd.WhatsApp.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.WhatsApp.Reminders;

public sealed class WhatsAppReminderProcessor(WhatsAppDbContext db, IProgramAccompanimentQuery programs,
    IWhatsAppMessageSender sender, ReminderSettings settings, TimeProvider clock)
{
    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var lease = now.AddMinutes(1);
        var rows = await db.Reminders.FromSqlInterpolated($"""
            UPDATE whatsapp.reminders SET lease_until = {lease}
            WHERE id = (SELECT id FROM whatsapp.reminders
                WHERE state = 'Pending' AND next_attempt_at <= {now}
                  AND (lease_until IS NULL OR lease_until < {now})
                ORDER BY next_attempt_at, id LIMIT 1 FOR UPDATE SKIP LOCKED)
            RETURNING *
            """).AsNoTracking().ToListAsync(ct);
        if (rows.Count == 0) return false;
        var reminder = rows[0];
        var link = await db.PatientLinks.AsNoTracking().SingleOrDefaultAsync(x => x.PatientId == reminder.PatientId, ct);
        var snapshot = await programs.GetSnapshotAsync([reminder.PatientId], now, ct);
        if (link is null || link.OptOut || (reminder.Kind == ReminderKind.checkin
            ? !snapshot.ActivePatients.Contains(reminder.PatientId) : !snapshot.Reasons.Any(x => x.SourceKey == reminder.SourceKey)))
        {
            await FinishAsync(reminder, ReminderState.Cancelled, "no_longer_eligible", now, ct);
            return true;
        }
        if (reminder.Attempts >= reminder.MaxAttempts)
        {
            await FinishAsync(reminder, ReminderState.Exhausted, "attempts_exhausted", now, ct);
            return true;
        }
        var inboundAt = await db.Conversations.AsNoTracking().Where(x => x.PatientId == reminder.PatientId)
            .Select(x => (DateTimeOffset?)x.LastInboundAt).SingleOrDefaultAsync(ct);
        if (inboundAt is { } inbound && inbound >= reminder.ScheduledAt)
        {
            await FinishAsync(reminder, ReminderState.Cancelled, "patient_interaction", now, ct);
            return true;
        }
        var allowed = ReminderSchedule.InWindow(now, settings);
        if (inboundAt?.AddMinutes(settings.ActiveConversationMinutes) is { } quietUntil && quietUntil > allowed)
            allowed = ReminderSchedule.InWindow(quietUntil, settings);
        {
            var last = await db.ReminderDeliveries.AsNoTracking().Where(x => x.PatientId == reminder.PatientId
                && x.Kind == reminder.Kind && x.AcceptedAt != null).MaxAsync(x => x.AcceptedAt, ct);
            if (last is { } accepted && accepted.AddHours(24) > allowed) allowed = ReminderSchedule.InWindow(accepted.AddHours(24), settings);
        }
        if (allowed > now)
        {
            await DeferAsync(reminder, allowed, ct);
            return true;
        }
        var delivery = new ReminderDelivery { ReminderId = reminder.Id, PatientId = reminder.PatientId,
            Kind = reminder.Kind, Attempt = reminder.Attempts + 1, AttemptedAt = now, State = DeliveryState.Dispatching };
        // Reserva intento + trazabilidad en una transacción corta; HTTP siempre después del commit.
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var updated = await db.Reminders.Where(x => x.Id == reminder.Id && x.State == ReminderState.Pending && x.LeaseUntil == lease)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.Attempts, delivery.Attempt), ct);
            if (updated == 0) return true;
            await db.ReminderDeliveries.Where(x => x.ReminderId == reminder.Id && x.State == DeliveryState.Dispatching)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.State, DeliveryState.Unknown).SetProperty(x => x.ErrorCode, "lease_expired"), ct);
            db.ReminderDeliveries.Add(delivery);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        // Releer preferencia/estado reduce la carrera con SALIR/completado mientras se reservaba el intento.
        if (!await db.Reminders.AnyAsync(x => x.Id == reminder.Id && x.State == ReminderState.Pending && x.LeaseUntil == lease, ct)
            || await db.PatientLinks.AnyAsync(x => x.PatientId == reminder.PatientId && x.OptOut, ct))
        {
            delivery.State = DeliveryState.Permanent;
            delivery.ErrorCode = "cancelled_before_send";
            await db.SaveChangesAsync(ct);
            await FinishAsync(reminder, ReminderState.Cancelled, "cancelled_before_send", now, ct);
            return true;
        }
        var name = (reminder.Kind, link.Language) switch
        {
            (ReminderKind.checkin, "en") => "wellness_checkin",
            (ReminderKind.checkin, _) => "checkin_bienestar",
            (ReminderKind.racha, "en") => "streak_reminder",
            (ReminderKind.racha, _) => "recordatorio_racha",
            (ReminderKind.podcast, "en") => "podcast_reminder",
            (ReminderKind.podcast, _) => "recordatorio_podcast",
            (_, "en") => "assessment_reminder",
            _ => "recordatorio_evaluacion",
        };
        WhatsAppSendResult result;
        try
        {
            result = await sender.SendTemplateAsync(link.WaId, name, link.Language,
                [link.FirstName, reminder.Detail], ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { result = WhatsAppSendResult.Failed("sender_unavailable", WhatsAppFailureKind.Transient); }
        delivery.Wamid = result.Wamid;
        delivery.ErrorCode = result.ErrorCode;
        if (result.Succeeded)
        {
            delivery.State = DeliveryState.Accepted;
            delivery.AcceptedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
            await FinishAsync(reminder, ReminderState.Completed, null, now, ct);
        }
        else
        {
            var retry = result.Failure == WhatsAppFailureKind.Transient && delivery.Attempt < reminder.MaxAttempts;
            delivery.State = result.Failure == WhatsAppFailureKind.Transient ? DeliveryState.Retryable : DeliveryState.Permanent;
            var next = ReminderSchedule.InWindow(clock.GetUtcNow() + ReminderSchedule.Backoff(delivery.Attempt, reminder.RepeatEveryMinutes), settings);
            delivery.NextAttemptAt = retry ? next : null;
            await db.SaveChangesAsync(ct);
            if (retry) await DeferAsync(reminder, next, ct);
            else await FinishAsync(reminder, result.Failure == WhatsAppFailureKind.Transient ? ReminderState.Exhausted : ReminderState.Cancelled,
                result.ErrorCode, now, ct);
        }
        return true;
    }

    private Task DeferAsync(Reminder reminder, DateTimeOffset next, CancellationToken ct) => db.Reminders
        .Where(x => x.Id == reminder.Id && x.State == ReminderState.Pending && x.LeaseUntil == reminder.LeaseUntil)
        .ExecuteUpdateAsync(set => set.SetProperty(x => x.NextAttemptAt, next).SetProperty(x => x.LeaseUntil, (DateTimeOffset?)null), ct);

    private Task FinishAsync(Reminder reminder, ReminderState state, string? reason, DateTimeOffset now, CancellationToken ct) => db.Reminders
        .Where(x => x.Id == reminder.Id && x.State == ReminderState.Pending && x.LeaseUntil == reminder.LeaseUntil)
        .ExecuteUpdateAsync(set => set.SetProperty(x => x.State, state).SetProperty(x => x.CancellationReason, reason)
            .SetProperty(x => x.LeaseUntil, (DateTimeOffset?)null), ct);
}
