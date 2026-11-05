using System.Text.Json;
using System.Text.Json.Nodes;
using CoppAddresd.Application.Features.ProgramProgress.Assignment;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

public sealed partial class ProgramRepository
{
    public async Task<AppliedAssignmentDto?> ActivateAssignmentFallbackAsync(AssignmentFallbackInput input, CancellationToken ct)
    {
        return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var identity = await dbContext.ProgramAssignmentRecommendations.AsNoTracking()
                    .Where(r => r.Id==input.RecommendationId)
                    .Select(r=>new { r.PatientId, r.EnrollmentId }).SingleOrDefaultAsync(ct);
                if (identity is null) return null;
                // Orden global de locks: perfil -> inscripción -> recomendación.
                var patient = await dbContext.PatientProfiles.FromSqlInterpolated(
                    $"SELECT * FROM app.patient_profiles WHERE id={identity.PatientId} FOR UPDATE").SingleAsync(ct);
                if (patient.DeletedAt is not null) return null;
                var enrollment = await dbContext.ProgramEnrollments.FromSqlInterpolated(
                    $"SELECT * FROM app.program_enrollments WHERE id={identity.EnrollmentId} FOR UPDATE").SingleAsync(ct);
                var recommendation = await dbContext.ProgramAssignmentRecommendations.FromSqlInterpolated(
                    $"SELECT * FROM app.program_assignment_recommendations WHERE id={input.RecommendationId} FOR UPDATE").SingleAsync(ct);
                if (recommendation.State is "applied" or "edited" or "fallback")
                {
                    var prior = JsonNode.Parse(recommendation.Proposal)?["appliedContent"]?.ToJsonString() ?? "{}";
                    return new AppliedAssignmentDto(recommendation.Id,enrollment.Id,patient.Id,recommendation.State,
                        enrollment.StartLocalDate,enrollment.CurrentWeekNumber,JsonSerializer.Deserialize<JsonElement>(prior),true,
                        new(enrollment.Id,enrollment.Status.ToString(),enrollment.CurrentWeekNumber,enrollment.StartLocalDate,enrollment.StartedAt,enrollment.Timezone));
                }
                if (recommendation.State=="dismissed")
                    throw new BusinessRuleViolationException("ASSIGNMENT_DISMISSED: la recomendación fue desestimada.");
                if (recommendation.CreatedAt>input.UtcNow.AddDays(-7)) return null;
                if (enrollment.Status!=ProgramEnrollmentStatus.PendingAssignment)
                    throw new BusinessRuleViolationException("INVALID_ENROLLMENT_STATE: la inscripción no está pendiente de asignación.");
                var template = await dbContext.ProgramTemplates.AsNoTracking().Include(t=>t.DayTemplates)
                    .SingleOrDefaultAsync(t=>t.Id==enrollment.TemplateId && t.Status==TemplateStatus.Active,ct)
                    ?? throw new BusinessRuleViolationException("TEMPLATE_NOT_ACTIVE: la plantilla no está activa.");
                var days = template.DayTemplates.OrderBy(d=>d.Weekday).ThenBy(d=>d.SortOrder).ToList();
                if (days.Count==0) throw new BusinessRuleViolationException("TEMPLATE_EMPTY: la plantilla no tiene tareas.");
                var proposal = JsonNode.Parse(recommendation.Proposal)!.AsObject();
                var zone = TimeZoneInfo.FindSystemTimeZoneById(patient.Timezone);
                var start = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(input.UtcNow,zone));
                var weeks = await dbContext.ProgramWeeks.Where(w=>w.EnrollmentId==enrollment.Id).ToListAsync(ct);
                var first = weeks.Single(w=>w.WeekNumber==1);
                foreach (var week in weeks)
                {
                    week.WeekStartDateLocal=start.AddDays((week.WeekNumber-1)*7);
                    week.WeekEndDateLocal=week.WeekStartDateLocal.AddDays(6);
                }
                first.Status=ProgramWeekStatus.Active; first.ActivatedAt=input.UtcNow;
                first.TasksSnapshot=BuildSnapshot(days);
                first.TemplateVersionAtStart=template.Version;
                enrollment.Status=ProgramEnrollmentStatus.Active; enrollment.CurrentWeekNumber=1;
                enrollment.StartLocalDate=start; enrollment.StartedAt=input.UtcNow; enrollment.Timezone=patient.Timezone;
                enrollment.UpdatedAt=input.UtcNow; enrollment.UpdatedBy=null;
                var content = JsonSerializer.SerializeToElement(new
                {
                    nutritionPlan = (object?)null,
                    routine = (object?)null,
                    templateDefaults=true, tasksSnapshot=first.TasksSnapshot,
                });
                // La propuesta original conserva matchedRules y los contenidos del motor; se agrega la decisión final.
                proposal["appliedContent"]=JsonNode.Parse(content.GetRawText());
                recommendation.Proposal=proposal.ToJsonString();
                recommendation.State="fallback";
                recommendation.AppliedBy=null; recommendation.AppliedAt=input.UtcNow; recommendation.UpdatedAt=input.UtcNow;
                if (assignmentNotices is not null)
                    await assignmentNotices.AddPatientNoticeAsync(recommendation.Id,patient.Id,input.UtcNow,ct);
                await dbContext.SaveChangesAsync(ct);
                await WriteAuditRowAsync("ProgramAssignmentFallback",
                    "app","program_assignment_recommendations",recommendation.Id,null,ct);
                await transaction.CommitAsync(ct);
                return new AppliedAssignmentDto(recommendation.Id,enrollment.Id,patient.Id,recommendation.State,start,1,content,false,
                    new(enrollment.Id,enrollment.Status.ToString(),1,start,enrollment.StartedAt,enrollment.Timezone));
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                dbContext.ChangeTracker.Clear();
                throw;
            }
        });
    }
}
