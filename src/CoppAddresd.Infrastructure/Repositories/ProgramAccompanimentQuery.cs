using CoppAddresd.Application.Features.ProgramProgress.Commands.BulkAssignProgramContent;
using CoppAddresd.Application.Features.Media;
using CoppAddresd.Application.Features.ProgramProgress.Queries;
using CoppAddresd.Domain.Entities.WhatsApp;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Enums.HealthTests;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

public sealed class ProgramAccompanimentQuery(AppDbContext db) : IProgramAccompanimentQuery
{
    public Task<Guid?> GetPatientIdAsync(Guid enrollmentId, CancellationToken ct) => db.ProgramEnrollments.AsNoTracking()
        .Where(x => x.Id == enrollmentId).Select(x => (Guid?)x.PatientId).SingleOrDefaultAsync(ct);

    public async Task<ProgramAccompanimentSnapshot> GetSnapshotAsync(IReadOnlyList<Guid> patientIds, DateTimeOffset now, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById("America/Bogota")).DateTime);
        var weekday = (short)(((int)today.DayOfWeek + 6) % 7 + 1);
        var valid = db.PatientProfiles.Where(p => patientIds.Contains(p.Id) && p.Status == "Activo" && p.DeletedAt == null).Select(p => p.Id);
        var enrollments = await db.ProgramEnrollments.AsNoTracking()
            .Where(e => valid.Contains(e.PatientId) && e.Status == ProgramEnrollmentStatus.Active)
            .Select(e => new { e.Id, e.PatientId, e.TemplateId, e.CurrentWeekNumber,
                CurrentStreak = (int?)e.StreakState!.CurrentStreak, LastActiveDate = e.StreakState.LastActiveDate }).ToListAsync(ct);
        var pending = await db.HealthTestAssignments.AsNoTracking()
            .Where(a => valid.Contains(a.PatientId)
                && (a.Status == HealthTestAssignmentStatus.pending || a.Status == HealthTestAssignmentStatus.in_progress)
                && (a.ExpiresAt == null || a.ExpiresAt > now.UtcDateTime)
                && a.BatteryAssignment != null && a.BatteryAssignment.Status != HealthTestAssignmentStatus.cancelled
                && a.BatteryAssignment.Battery!.Code == "bateria-inicial"
                && a.BatteryAssignment.Battery.IsActive && a.BatteryAssignment.Battery.AutoAssignOnPatientCreate
                && !db.HealthTestEvaluations.Any(e => e.AssignmentId == a.Id && e.PatientId == a.PatientId && e.Status == HealthTestEvaluationStatus.completed))
            .Select(a => new { a.Id, a.PatientId }).ToListAsync(ct);
        List<ProgramReminderReason> reasons = pending.Select(a => new ProgramReminderReason(a.PatientId, ReminderKind.evaluacion,
            $"evaluation:{a.Id:N}", "Tu evaluación inicial del programa está pendiente. Complétala en la app ANTARES.",
            "Your initial program assessment is pending. Complete it in the ANTARES app.", a.Id)).ToList();
        foreach (var enrollment in enrollments.Where(e => e.CurrentStreak > 0 && e.LastActiveDate == today.AddDays(-1)))
            reasons.Add(new(enrollment.PatientId, ReminderKind.racha, $"streak:{enrollment.Id:N}:{today:yyyy-MM-dd}",
                $"Tu racha de {enrollment.CurrentStreak} días tiene actividad pendiente hoy. Revisa tu programa en ANTARES.",
                $"Your {enrollment.CurrentStreak}-day streak has activity pending today. Check your ANTARES program."));
        var enrollmentIds = enrollments.Select(e => e.Id).ToArray();
        var templates = enrollments.Select(e => e.TemplateId).Distinct().ToArray();
        var weeks = await db.ProgramWeeks.AsNoTracking().Where(w => enrollmentIds.Contains(w.EnrollmentId)
            && w.WeekStartDateLocal <= today && w.WeekEndDateLocal >= today && w.Status == ProgramWeekStatus.Active)
            .Select(w => new { w.EnrollmentId, w.TasksSnapshot }).ToListAsync(ct);
        var completed = await db.TaskCompletions.AsNoTracking().Where(t => enrollmentIds.Contains(t.EnrollmentId)
            && t.LocalDate == today && t.TaskCode == TaskCode.podcast).Select(t => t.EnrollmentId).ToListAsync(ct);
        var fallback = await db.WeeklyDayTemplates.AsNoTracking().Where(d => templates.Contains(d.TemplateId)
            && d.Weekday == weekday && d.TaskCode == TaskCode.podcast).Select(d => new { d.TemplateId, d.MediaId }).ToListAsync(ct);
        var assigned = weeks.Where(w => !completed.Contains(w.EnrollmentId)).SelectMany(w =>
        {
            var enrollment = enrollments.Single(e => e.Id == w.EnrollmentId);
            return WeekSnapshotJson.Parse(w.TasksSnapshot).Where(t => t.Weekday == weekday && t.TaskCode == "podcast")
                .Select(t => new { enrollment.PatientId, enrollment.Id, MediaId = t.MediaId ?? fallback.FirstOrDefault(f => f.TemplateId == enrollment.TemplateId)?.MediaId });
        }).Where(x => x.MediaId != null).ToList();
        var mediaIds = assigned.Select(x => x.MediaId!.Value).Distinct().ToArray();
        var published = await db.MediaItems.AsNoTracking().Where(m => mediaIds.Contains(m.Id) && m.Status == MediaStatus.Published
            && m.MediaType == MediaType.Podcast && m.StorageKey.Trim().ToLower() != MediaPublishRules.DemoStorageKey)
            .Select(m => new { m.Id, m.Title }).ToDictionaryAsync(m => m.Id, m => m.Title, ct);
        foreach (var item in assigned.Where(x => published.ContainsKey(x.MediaId!.Value)))
        {
            var title = published[item.MediaId!.Value];
            if (title.Length > 200) title = title[..200];
            reasons.Add(new(item.PatientId, ReminderKind.podcast, $"podcast:{item.Id:N}:{today:yyyy-MM-dd}:{item.MediaId:N}",
                $"Tienes pendiente el podcast asignado «{title}». Escúchalo en la app ANTARES.",
                $"Your assigned podcast “{title}” is pending. Listen in the ANTARES app."));
        }
        return new(enrollments.Select(e => e.PatientId).ToHashSet(), reasons);
    }
}
