using System.Text.Json;
using CoppAddresd.Application.Features.ProgramProgress.Queries;
using CoppAddresd.Domain.Entities.WhatsApp;
using CoppAddresd.WhatsApp.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.WhatsApp.Reminders;

public sealed class WhatsAppReminderScheduler(WhatsAppDbContext db, IProgramAccompanimentQuery programs,
    ReminderSettings settings, TimeProvider clock)
{
    public async Task<Guid?> ScheduleBatchAsync(Guid? after, CancellationToken ct, IReadOnlyList<Guid>? patientIds = null)
    {
        var now = clock.GetUtcNow();
        var links = await db.PatientLinks.AsNoTracking().Where(x => (after == null || x.Id.CompareTo(after.Value) > 0)
            && (patientIds == null || patientIds.Contains(x.PatientId)))
            .OrderBy(x => x.Id).Take(100).ToListAsync(ct);
        if (links.Count == 0) return null;
        var ids = links.Select(x => x.PatientId).ToArray();
        var snapshot = await programs.GetSnapshotAsync(ids, now, ct);
        var active = snapshot.ActivePatients.ToArray();
        var eligible = links.Where(x => !x.OptOut && (snapshot.ActivePatients.Contains(x.PatientId)
            || snapshot.Reasons.Any(r => r.PatientId == x.PatientId))).Select(x => x.PatientId).ToArray();
        var reasons = snapshot.Reasons.Select(x => x.SourceKey).ToArray();
        // Reconciliación durable: cubre fallos/reinicios del hook después del commit clínico.
        await db.Reminders.Where(r => ids.Contains(r.PatientId) && r.State == ReminderState.Pending
            && (!eligible.Contains(r.PatientId) || r.Kind == ReminderKind.checkin && !active.Contains(r.PatientId)
                || r.Kind != ReminderKind.checkin && !reasons.Contains(r.SourceKey)))
            .ExecuteUpdateAsync(set => set.SetProperty(r => r.State, ReminderState.Cancelled)
                .SetProperty(r => r.CancellationReason, "no_longer_eligible").SetProperty(r => r.LeaseUntil, (DateTimeOffset?)null), ct);
        var inbound = await db.Conversations.AsNoTracking().Where(x => ids.Contains(x.PatientId))
            .ToDictionaryAsync(x => x.PatientId, x => x.LastInboundAt, ct);
        var accepted = await db.ReminderDeliveries.AsNoTracking().Where(x => ids.Contains(x.PatientId)
            && x.Kind == ReminderKind.checkin && x.AcceptedAt != null)
            .GroupBy(x => x.PatientId).Select(group => new { PatientId = group.Key, At = group.Max(x => x.AcceptedAt) })
            .ToDictionaryAsync(x => x.PatientId, x => x.At, ct);
        List<Reminder> candidates = [];
        foreach (var link in links.Where(x => eligible.Contains(x.PatientId) && active.Contains(x.PatientId)))
        {
            var lastInbound = inbound.GetValueOrDefault(link.PatientId);
            var quietUntil = lastInbound.AddMinutes(settings.ActiveConversationMinutes);
            var checkin = ReminderSchedule.Daily(now, accepted.GetValueOrDefault(link.PatientId), settings);
            if (quietUntil > checkin) checkin = ReminderSchedule.InWindow(quietUntil, settings);
            var date = TimeZoneInfo.ConvertTime(checkin, ReminderSchedule.Zone).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            candidates.Add(new Reminder { PatientId = link.PatientId, Kind = ReminderKind.checkin,
                SourceKey = $"checkin:{link.PatientId:N}:{date}", ScheduledAt = checkin, NextAttemptAt = checkin,
                Detail = link.Language == "en" ? $"Your program wellness check-in for {date}: how are you feeling today?"
                    : $"Tu check-in de bienestar del programa para {date}: ¿cómo estás?, ¿cómo te sientes?" });
        }
        foreach (var reason in snapshot.Reasons.Where(x => eligible.Contains(x.PatientId)))
        {
            var quietUntil = inbound.GetValueOrDefault(reason.PatientId).AddMinutes(settings.ActiveConversationMinutes);
            var scheduled = ReminderSchedule.InWindow(quietUntil > now ? quietUntil : now, settings);
            var link = links.Single(x => x.PatientId == reason.PatientId);
            candidates.Add(new Reminder { PatientId = reason.PatientId, Kind = reason.Kind,
                AssignmentId = reason.AssignmentId, SourceKey = reason.SourceKey, ScheduledAt = scheduled, NextAttemptAt = scheduled,
                Detail = link.Language == "en" ? reason.DetailEn : reason.DetailEs });
        }
        if (candidates.Count > 0)
        {
            var json = JsonSerializer.Serialize(candidates.Select(r => new { id = r.Id, patient_id = r.PatientId,
                kind = r.Kind.ToString(), assignment_id = r.AssignmentId, source_key = r.SourceKey, detail = r.Detail,
                scheduled_at = r.ScheduledAt, next_attempt_at = r.NextAttemptAt }));
            // Un solo INSERT por lote; los índices resuelven dos schedulers concurrentes sin check-then-act.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO whatsapp.reminders
                  (id, patient_id, kind, assignment_id, source_key, detail, scheduled_at, next_attempt_at, repeat_every_minutes, max_attempts, attempts, state)
                SELECT id, patient_id, kind, assignment_id, source_key, detail, scheduled_at, next_attempt_at, 120, 5, 0, 'Pending'
                FROM jsonb_to_recordset(CAST({json} AS jsonb)) AS x(id uuid, patient_id uuid, kind text,
                  assignment_id uuid, source_key text, detail text, scheduled_at timestamptz, next_attempt_at timestamptz)
                ON CONFLICT DO NOTHING
                """, ct);
        }
        return links.Count == 100 ? links[^1].Id : null;
    }
}
