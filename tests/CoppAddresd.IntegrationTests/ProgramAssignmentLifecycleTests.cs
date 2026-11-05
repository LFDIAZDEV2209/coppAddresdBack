using CoppAddresd.Application.Features.ProgramProgress.Assignment;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Enums.HealthTests;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Infrastructure.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoppAddresd.IntegrationTests;

public sealed partial class ProgramAssignmentIntegrationTests
{
    private readonly LifecycleClock _lifecycleClock=new();
    [AssignmentPostgresFact]
    public async Task Inbox_ConservaAlcanceYCopy_YRedirigeAvisosHistoricosAlPerfil()
    {
        var data = await ScenarioAsync(2); await using var db = NewDb();
        var notices = new ProgramAssignmentNoticeRepository(db);
        var expected = $"/program/gestion?view=perfil-360&patient={data.PatientId}";
        Assert.Equal(expected, (await db.ProgramAssignmentNotices.SingleAsync()).Link);
        await db.ProgramAssignmentNotices.ExecuteUpdateAsync(s => s.SetProperty(n => n.Link, "/program/" + "asignacion/" + data.RecommendationId));
        var inbox = await notices.ListInboxAsync(data.ProfessionalUser,1,20,CancellationToken.None);
        var notice = Assert.Single(inbox.Items); Assert.Equal(expected,notice.Link);
        Assert.Contains("completed the initial assessment",notice.MessageEn);
        Assert.Empty((await notices.ListInboxAsync(data.OtherUser,1,20,CancellationToken.None)).Items);
    }

    private sealed class LifecycleClock : TimeProvider
    {
        public DateTimeOffset Current=new(2026,11,4,15,0,0,TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow()=>Current;
    }
    private sealed record Scenario(Guid PatientId,Guid EnrollmentId,Guid RecommendationId,Guid ProfessionalUser,Guid OtherUser);

    private async Task<Scenario> ScenarioAsync(int waitingDays,bool recommendation=true)
    {
        await using var db=NewDb();
        var actor=Guid.NewGuid();var other=Guid.NewGuid();var patientUser=Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO auth.\"Users\" (\"Id\") VALUES ({actor}),({other}),({patientUser})");
        var organization=new Organization { Id=Guid.NewGuid(),Code=Guid.NewGuid().ToString("N"),Name="Prueba" };
        var employee=new Employee { Id=Guid.NewGuid(),OrganizationId=organization.Id,UserId=actor,
            FirstName="Profesional",LastName="A",Email=Guid.NewGuid()+"@test.local",Status="Active" };
        var employeeB=new Employee { Id=Guid.NewGuid(),OrganizationId=organization.Id,UserId=other,
            FirstName="Profesional",LastName="B",Email=Guid.NewGuid()+"@test.local",Status="Active" };
        var professional=new Professional { Id=Guid.NewGuid(),EmployeeId=employee.Id };
        var professionalB=new Professional { Id=Guid.NewGuid(),EmployeeId=employeeB.Id };
        var patient=new PatientProfile { Id=Guid.NewGuid(),UserId=patientUser,FirstName="Paciente",LastName="Miércoles",
            DocumentNumber=Guid.NewGuid().ToString("N"),Timezone="America/New_York" };
        db.Organizations.Add(organization);db.Employees.AddRange(employee,employeeB);db.Professionals.AddRange(professional,professionalB);
        db.PatientProfiles.Add(patient);db.PatientProfessionalAssignments.Add(new() { PatientId=patient.Id,ProfessionalId=professional.Id });
        var template=await db.ProgramTemplates.SingleAsync();
        var plan=await db.NutritionPlans.SingleAsync();var routine=await db.ExerciseRoutines.SingleAsync();
        db.NutritionPlans.Add(new() { Id=Guid.NewGuid(),Name="Plan Alternativo",DurationDays=7,Status=NutritionPlanStatus.Active,IsTemplate=true });
        db.ExerciseRoutines.Add(new() { Id=Guid.NewGuid(),Name="Full Body",Difficulty=(RoutineDifficulty)2,Status=NutritionPlanStatus.Active });
        for (short day=1;day<=7;day++)
        {
            db.WeeklyDayTemplates.Add(new() { Id=Guid.NewGuid(),TemplateId=template.Id,Weekday=day,TaskCode=TaskCode.nut,Points=10,SortOrder=1,NutritionPlanId=plan.Id });
            db.WeeklyDayTemplates.Add(new() { Id=Guid.NewGuid(),TemplateId=template.Id,Weekday=day,TaskCode=TaskCode.ejercicio,Points=10,SortOrder=2,RoutineId=routine.Id });
            db.WeeklyDayTemplates.Add(new() { Id=Guid.NewGuid(),TemplateId=template.Id,Weekday=day,TaskCode=TaskCode.emocional,Points=10,SortOrder=3 });
        }
        await db.SaveChangesAsync();
        using var scope=_services.CreateScope();
        var repository=scope.ServiceProvider.GetRequiredService<IProgramAssignmentRepository>();
        var programs=scope.ServiceProvider.GetRequiredService<IProgramRepository>();
        var recId=Guid.NewGuid();Guid enrollmentId=default;
        await repository.ExecuteForPatientAsync(patient.Id,async ()=>
        {
            var enrollment=await programs.CreatePendingAssignmentAsync(patient.Id,template.Code,CancellationToken.None);
            enrollmentId=enrollment.Id;
            if (!recommendation) return;
            await repository.AddRecommendationAsync(new ProgramAssignmentRecommendation
            {
                Id=recId,EnrollmentId=enrollment.Id,PatientId=patient.Id,EngineVersion="test-v1",
                CreatedAt=_lifecycleClock.Current.UtcDateTime.AddDays(-waitingDays),
                ProfileSnapshot="""{"results":[{"severity":"high"}],"facts":{}}""",
                Proposal="""{"nutritionPlan":{"name":"Plan Obesidad — Déficit calórico"},"routine":{"name":"Caminata Activa"},"matchedRules":[]}""",
            },CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<IProgramAssignmentNoticeRepository>()
                .AddProfessionalNoticesAsync(recId,patient.Id,_lifecycleClock.Current.UtcDateTime,CancellationToken.None);
        },CancellationToken.None);
        return new(patient.Id,enrollmentId,recId,actor,other);
    }

    [AssignmentPostgresFact]
    public async Task MigracionRecoveryAvisos_DownYReaplicacionEnBdAislada()
    {
        await using var db=NewDb();
        var migrator=Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>(db);
        await migrator.MigrateAsync("20261104154406_AddProgramAssignmentPatientContext");
        Assert.Equal(0,await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM information_schema.tables WHERE table_schema='app' AND table_name IN ('program_assignment_notices','program_assignment_recoveries')").SingleAsync());
        await migrator.MigrateAsync("20261104161744_AddProgramAssignmentRecoveryAndNotices");
        Assert.Equal(2,await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM information_schema.tables WHERE table_schema='app' AND table_name IN ('program_assignment_notices','program_assignment_recoveries')").SingleAsync());
    }

    [AssignmentPostgresFact]
    public async Task Job_Dia8_DefaultsDePlantilla_ActivaUnaVezYSinDuplicarAvisos()
    {
        var data=await ScenarioAsync(8);
        using var scope=_services.CreateScope();
        var job=scope.ServiceProvider.GetRequiredService<ProgramAssignmentJob>();
        Assert.Equal(1,(await job.RunAsync(CancellationToken.None)).Fallbacks);
        Assert.Equal(0,(await job.RunAsync(CancellationToken.None)).Fallbacks);
        Assert.True((await scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new ApplyAssignmentFallbackCommand(data.RecommendationId), CancellationToken.None))!.Replay);
        await using var db=NewDb();
        var rec=await db.ProgramAssignmentRecommendations.AsNoTracking().SingleAsync(r=>r.Id==data.RecommendationId);
        Assert.Equal("fallback",rec.State);Assert.Null(rec.AppliedBy);
        var first=await db.ProgramWeeks.AsNoTracking().SingleAsync(w=>w.EnrollmentId==data.EnrollmentId && w.WeekNumber==1);
        var defaults=await db.WeeklyDayTemplates.AsNoTracking().Where(t=>t.TaskCode==TaskCode.nut).Select(t=>t.NutritionPlanId).Distinct().SingleAsync();
        Assert.All(first.TasksSnapshot.EnumerateArray().Where(t=>t.GetProperty("task_code").GetString()=="nut"),
            t=>Assert.Equal(defaults,t.GetProperty("nutrition_plan_id").GetGuid()));
        Assert.Equal(1,await db.ProgramAssignmentNotices.CountAsync(n=>n.RecommendationId==data.RecommendationId && n.Kind=="patient-ready"));
        var noticeId=await db.ProgramAssignmentNotices.Where(n=>n.RecommendationId==data.RecommendationId && n.Kind=="patient-ready").Select(n=>n.Id).SingleAsync();
        await using var dbA=NewDb();await using var dbB=NewDb();
        var claims=await Task.WhenAll(new ProgramAssignmentNoticeRepository(dbA).TryClaimAsync(noticeId,_lifecycleClock.Current.UtcDateTime,CancellationToken.None),
            new ProgramAssignmentNoticeRepository(dbB).TryClaimAsync(noticeId,_lifecycleClock.Current.UtcDateTime,CancellationToken.None));
        Assert.Single(claims,c=>c is not null);
        var notices=new ProgramAssignmentNoticeRepository(db);
        await notices.FinishAsync(noticeId,true,false,_lifecycleClock.Current.UtcDateTime,CancellationToken.None);
        Assert.Null(await notices.TryClaimAsync(noticeId,_lifecycleClock.Current.UtcDateTime,CancellationToken.None));
        var retryTime=_lifecycleClock.Current.UtcDateTime.AddMinutes(5);
        var retry=await notices.TryClaimAsync(noticeId,retryTime,CancellationToken.None);
        Assert.True(retry!.PushCompleted);Assert.False(retry.ChatCompleted);
        await notices.FinishAsync(noticeId,true,true,retryTime,CancellationToken.None);
        Assert.Null(await notices.TryClaimAsync(noticeId,retryTime.AddHours(1),CancellationToken.None));
    }

    [AssignmentPostgresFact]
    public async Task Job_RecuperaInscripcionPendienteSinRecomendacion_ClaimYBackoffPersistidos()
    {
        var data=await ScenarioAsync(0,false);
        await using var db=NewDb();
        var battery=await db.HealthTestBatteries.SingleAsync(b=>b.Code=="bateria-inicial");battery.AutoAssignOnPatientCreate=true;
        var parent=new HealthTestBatteryAssignment { Id=Guid.NewGuid(),PatientId=data.PatientId,BatteryId=battery.Id,Status=HealthTestAssignmentStatus.completed };
        db.HealthTestBatteryAssignments.Add(parent);
        var instruments=await db.HealthTestBatteryItems.Where(i=>i.BatteryId==battery.Id && i.IsRequired).Select(i=>i.InstrumentId).ToListAsync();
        var versions=await db.HealthTestVersions.Where(v=>instruments.Contains(v.InstrumentId) && v.IsCurrent).ToListAsync();
        foreach(var version in versions)
        {
            var assignment=new HealthTestAssignment { Id=Guid.NewGuid(),PatientId=data.PatientId,BatteryAssignmentId=parent.Id,VersionId=version.Id,Status=HealthTestAssignmentStatus.completed };
            db.HealthTestAssignments.Add(assignment);
            db.HealthTestEvaluations.Add(new() { Id=Guid.NewGuid(),AssignmentId=assignment.Id,PatientId=data.PatientId,VersionId=version.Id,Status=HealthTestEvaluationStatus.completed,CompletedAt=_lifecycleClock.Current.UtcDateTime });
        }
        await db.SaveChangesAsync();
        using var scope=_services.CreateScope();
        Assert.Equal(1,(await scope.ServiceProvider.GetRequiredService<ProgramAssignmentJob>().RunAsync(CancellationToken.None)).Recovered);
        Assert.Equal(1,await db.ProgramEnrollments.CountAsync(e=>e.PatientId==data.PatientId));
        Assert.Equal(1,await db.ProgramAssignmentRecommendations.CountAsync(r=>r.EnrollmentId==data.EnrollmentId));
        var recovery=await db.ProgramAssignmentRecoveries.AsNoTracking().SingleAsync();
        Assert.Equal(1,recovery.Attempts);Assert.True(recovery.NextAttemptAt>_lifecycleClock.Current.UtcDateTime);
        var operations=scope.ServiceProvider.GetRequiredService<IProgramAssignmentOperationsRepository>();
        Assert.False(await operations.TryClaimRecoveryAsync(parent.Id,_lifecycleClock.Current.UtcDateTime,2,60,CancellationToken.None));
        var next=_lifecycleClock.Current.UtcDateTime.AddSeconds(60);
        Assert.True(await operations.TryClaimRecoveryAsync(parent.Id,next,2,60,CancellationToken.None));
        var second=await db.ProgramAssignmentRecoveries.AsNoTracking().SingleAsync();
        Assert.Equal(next.AddSeconds(120),second.NextAttemptAt);
        Assert.False(await operations.TryClaimRecoveryAsync(parent.Id,next.AddHours(1),2,60,CancellationToken.None));
    }
}
