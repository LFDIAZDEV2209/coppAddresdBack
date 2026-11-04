using System.Text.Json;
using System.Text.Json.Nodes;
using CoppAddresd.Application.Features.ProgramProgress.Assignment;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

public sealed partial class ProgramRepository
{
    public async Task<AppliedAssignmentDto?> ActivateAssignmentAsync(AssignmentActivationInput input, CancellationToken ct)
    {
        return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var identity = await dbContext.ProgramAssignmentRecommendations.AsNoTracking()
                    .Where(r => r.Id==input.RecommendationId && (input.ScopedPatientIds==null || input.ScopedPatientIds.Contains(r.PatientId)))
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
                if (input.UseTemplateDefaults && recommendation.CreatedAt>input.UtcNow.AddDays(-7)) return null;
                if (enrollment.Status!=ProgramEnrollmentStatus.PendingAssignment)
                    throw new BusinessRuleViolationException("INVALID_ENROLLMENT_STATE: la inscripción no está pendiente de asignación.");
                var template = await dbContext.ProgramTemplates.AsNoTracking().Include(t=>t.DayTemplates)
                    .SingleOrDefaultAsync(t=>t.Id==enrollment.TemplateId && t.Status==TemplateStatus.Active,ct)
                    ?? throw new BusinessRuleViolationException("TEMPLATE_NOT_ACTIVE: la plantilla no está activa.");
                var days = template.DayTemplates.OrderBy(d=>d.Weekday).ThenBy(d=>d.SortOrder).ToList();
                if (days.Count==0) throw new BusinessRuleViolationException("TEMPLATE_EMPTY: la plantilla no tiene tareas.");
                var proposal = JsonNode.Parse(recommendation.Proposal)!.AsObject();
                var planName = input.UseTemplateDefaults ? null : input.OverrideNutritionPlanName ?? proposal["nutritionPlan"]?["name"]?.GetValue<string>();
                var routineName = input.UseTemplateDefaults ? null : input.OverrideRoutineName ?? proposal["routine"]?["name"]?.GetValue<string>();
                var plan = planName is null ? null : await dbContext.NutritionPlans.AsNoTracking()
                    .Where(p=>p.Name==planName && p.Status==NutritionPlanStatus.Active && (p.IsTemplate || p.PatientId==patient.Id))
                    .OrderBy(p=>p.Id).FirstOrDefaultAsync(ct)
                    ?? (planName is null ? null : throw new BusinessRuleViolationException("ASSIGNMENT_CONTENT_MISSING: el plan ya no está disponible."));
                var routine = routineName is null ? null : await dbContext.ExerciseRoutines.AsNoTracking()
                    .Where(r=>r.Name==routineName && r.Status==NutritionPlanStatus.Active).OrderBy(r=>r.Id).FirstOrDefaultAsync(ct)
                    ?? (routineName is null ? null : throw new BusinessRuleViolationException("ASSIGNMENT_CONTENT_MISSING: la rutina ya no está disponible."));
                foreach (var day in days)
                {
                    if (day.TaskCode==TaskCode.nut && plan is not null) day.NutritionPlanId=plan.Id;
                    if (day.TaskCode==TaskCode.ejercicio && routine is not null) day.RoutineId=routine.Id;
                }
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
                enrollment.UpdatedAt=input.UtcNow; enrollment.UpdatedBy=input.ActorId;
                var content = JsonSerializer.SerializeToElement(new
                {
                    nutritionPlan = plan is null ? null : new { id=plan.Id,name=plan.Name },
                    routine = routine is null ? null : new { id=routine.Id,name=routine.Name },
                    templateDefaults=input.UseTemplateDefaults, tasksSnapshot=first.TasksSnapshot,
                });
                // La propuesta original conserva matchedRules y los contenidos del motor; se agrega la decisión final.
                proposal["appliedContent"]=JsonNode.Parse(content.GetRawText());
                recommendation.Proposal=proposal.ToJsonString();
                recommendation.State=input.UseTemplateDefaults ? "fallback" :
                    input.OverrideNutritionPlanName is not null || input.OverrideRoutineName is not null ? "edited" : "applied";
                recommendation.AppliedBy=input.ActorId; recommendation.AppliedAt=input.UtcNow; recommendation.UpdatedAt=input.UtcNow;
                var startUtc=DateTime.SpecifyKind(start.ToDateTime(TimeOnly.MinValue),DateTimeKind.Utc);
                var endUtc=DateTime.SpecifyKind(start.AddDays(6).ToDateTime(TimeOnly.MaxValue),DateTimeKind.Utc);
                if (plan is not null) dbContext.NutritionPlanAssignments.Add(new NutritionPlanAssignment
                {
                    Id=Guid.NewGuid(), PatientId=patient.Id, PlanId=plan.Id, StartDate=startUtc, EndDate=endUtc,
                    Status=AssignmentStatus.Active, CreatedBy=input.ActorId, CreatedAt=input.UtcNow,
                });
                if (routine is not null) dbContext.RoutineAssignments.Add(new RoutineAssignment
                {
                    Id=Guid.NewGuid(), PatientId=patient.Id, RoutineId=routine.Id, StartDate=startUtc, EndDate=endUtc,
                    Status=AssignmentStatus.Active, CreatedBy=input.ActorId, CreatedAt=input.UtcNow,
                });
                if (assignmentNotices is not null)
                    await assignmentNotices.AddPatientNoticeAsync(recommendation.Id,patient.Id,input.UtcNow,ct);
                await dbContext.SaveChangesAsync(ct);
                await WriteAuditRowAsync(input.UseTemplateDefaults ? "ProgramAssignmentFallback" : "ProgramAssignmentApplied",
                    "app","program_assignment_recommendations",recommendation.Id,input.ActorId,ct);
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
