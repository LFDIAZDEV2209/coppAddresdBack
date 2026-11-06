using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.Nutrition.Cases;
using CoppAddresd.Application.Services.ProgramProgress;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Persistence;
using CoppAddresd.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.IntegrationTests;

public sealed partial class NutritionPlanWorkflowTests
{
    private static readonly DateOnly MissionToday = new(2026, 11, 5);
    private static ProgramContentResolver MissionResolver(AppDbContext db)
        => new(new WellnessRepository(db), NullLogger<ProgramContentResolver>.Instance, new NutritionPrescriptionRepository(db));
    private static ProgramRepository MissionRepository(AppDbContext db)
        => new(db, new ConfigurationBuilder().Build(), programContentResolver: MissionResolver(db));

    private async Task<(Guid Patient, Guid Enrollment, NutritionPrescription Prescription, Guid? Wellness)> MissionSeedAsync(AppDbContext db, bool wellness)
    {
        var patient=await PatientAsync(db);
        var template=new ProgramTemplate { Id=Guid.NewGuid(),Code="nutrition-mission",Name="Nutrición",Status=TemplateStatus.Active,TotalWeeks=6 };
        template.DayTemplates=Enumerable.Range(1,7).Select(day=>new WeeklyDayTemplate {
            Id=Guid.NewGuid(),Weekday=(short)day,TaskCode=TaskCode.nut,Points=150,SortOrder=1 }).ToList();
        db.ProgramTemplates.Add(template);
        db.HabitTemplates.AddRange(new[]{"des","alm","mer","cen"}.Select((code,index)=>new HabitTemplate {
            Id=Guid.NewGuid(),Code=code,Name=code,Category="alimentacion",SortOrder=index }));
        var food=new NutritionFoodItem { Id="mission-test",Name="Alimento de prueba",FoodGroup="C",Calories100=999,
            Carbs100=20,Protein100=10,Fat100=5,Fiber100=3 };
        db.NutritionFoodItems.Add(food);
        var plan=InboxPlan(patient.Id,InboxNow); PublishInboxPlan(plan,MissionToday);
        plan.CalorieTarget=2110; plan.ProteinTarget=110; plan.CarbsTarget=240; plan.FatTarget=70; plan.FiberTarget=30;
        plan.WaterMl=2000; plan.Instructions="Indicación del plan";
        plan.Meals=Enumerable.Range(1,30).SelectMany(day=>new[]{"des","alm","mer","cen"}.Select((code,index)=>
            new NutritionPrescriptionMeal { DayNumber=day,MealType=code,ScheduledTime=new[]{new TimeOnly(7,0),new TimeOnly(12,0),new TimeOnly(15,30),new TimeOnly(19,0)}[index],
                MenuName=$"Prescrito {day} {code}",WaterMl=250,Notes="Nota prescrita",
                Items=[new NutritionPrescriptionItem { FoodItem=food,FoodItemId=food.Id,Grams=100,SortOrder=1 }] })).ToList();
        db.NutritionPrescriptions.Add(plan);
        Guid? wellnessId=null;
        if (wellness)
        {
            var legacy=new NutritionPlan { Id=Guid.NewGuid(),PatientId=patient.Id,IsTemplate=false,Name="Bienestar anterior",
                DurationDays=30,ReviewStatus="approved",Status=NutritionPlanStatus.Active,ApprovedAt=InboxNow,ApprovedBy=patient.UserId,
                Days=Enumerable.Range(1,7).Select(day=>new NutritionPlanDay { Id=Guid.NewGuid(),DayNumber=day,
                    MealType=MealType.Desayuno,Description="Comida wellness",SortOrder=1 }).ToList() };
            db.NutritionPlans.Add(legacy); wellnessId=legacy.Id;
            db.NutritionPlanAssignments.Add(new NutritionPlanAssignment { Id=Guid.NewGuid(),PatientId=patient.Id,PlanId=legacy.Id,
                Status=AssignmentStatus.Active,StartDate=MissionToday.ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc) });
        }
        await db.SaveChangesAsync();
        var enrollment=await MissionRepository(db).EnrollAsync(patient.Id,template.Id,"America/Bogota",MissionToday,ct:CancellationToken.None);
        return (patient.Id,enrollment.Id,plan,wellnessId);
    }

    [NutritionPostgresFact]
    public Task Mission_PrescriptionWinsOverWellness_AtTheFinalConsumer() => CheckMissionAsync(true);

    [NutritionPostgresFact]
    public Task Mission_WithoutWellnessAssignment_HasPrescribedMealsAndAdherenceGate() => CheckMissionAsync(false);

    private async Task CheckMissionAsync(bool wellness)
    {
        await using var db=Context(); var seed=await MissionSeedAsync(db,wellness);
        var repo=MissionRepository(db);
        var snapshot=await repo.GetSnapshotAsync(seed.Enrollment,MissionToday,CancellationToken.None);
        var task=Assert.Single(snapshot!.TodayTasks);
        Assert.False(task.ContentUnavailable); var content=Assert.IsType<TodayTaskContentDto>(task.Content);
        Assert.False(content.ContentUnavailable); Assert.Null(content.NutritionPlanId); Assert.Equal(30,content.NutritionPlanDayNumber);
        Assert.Equal(2110,content.DailyCalorieTarget); Assert.Equal(110m,content.DailyProteinTarget);
        Assert.Equal(240m,content.DailyCarbsTarget); Assert.Equal(70m,content.DailyFatTarget); Assert.Equal(30m,content.DailyFiberTarget);
        Assert.Equal(new[]{"Desayuno","Almuerzo","Snack","Cena"},content.NutritionMeals!.Select(m=>m.MealType));
        Assert.All(content.NutritionMeals!,m=> {
            Assert.StartsWith("Prescrito 30",m.Description); Assert.Equal("Alimento de prueba x 100 g",m.Foods);
            Assert.Equal(165,m.Calories); Assert.Equal(10m,m.ProteinG); Assert.Equal(20m,m.CarbsG);
            Assert.Equal(5m,m.FatG); Assert.Equal(3m,m.FiberG); Assert.Equal(250,m.WaterMl); Assert.Equal("Nota prescrita",m.Notes);
        });
        Assert.Equal(new[]{0,1,2,3},content.NutritionMeals!.Select(m=>m.SortOrder));
        var input=new CompleteTaskInput(seed.Enrollment,MissionToday,TaskCode.nut,"mission-nut",null,null,null,null,
            NutritionPlanId:seed.Wellness,NutritionPlanDayNumber:1);
        var missing=await Assert.ThrowsAsync<NutritionEvidenceRequiredException>(()=>repo.CompleteTaskAsync(input,CancellationToken.None));
        Assert.Equal(new[]{"des","alm","mer","cen"},missing.MissingMealCodes);
        Assert.False(await db.TaskCompletions.AnyAsync());
        // El log real vuelve a usar el resolver y conserva nulas sus propias FKs wellness.
        foreach (var code in new[]{MealCode.des,MealCode.alm,MealCode.mer})
            await MissionRepository(db).LogNutritionAsync(seed.Patient,code,MissionToday,ct:CancellationToken.None);
        missing=await Assert.ThrowsAsync<NutritionEvidenceRequiredException>(()=>MissionRepository(db).CompleteTaskAsync(input,CancellationToken.None));
        Assert.Equal(new[]{"cen"},missing.MissingMealCodes);
        await MissionRepository(db).LogNutritionAsync(seed.Patient,MealCode.cen,MissionToday,ct:CancellationToken.None);
        var first=await MissionRepository(db).CompleteTaskAsync(input,CancellationToken.None);
        var replay=await MissionRepository(db).CompleteTaskAsync(input,CancellationToken.None);
        Assert.Equal(CompleteTaskOutcome.Created,first.Outcome); Assert.Equal(CompleteTaskOutcome.Replay,replay.Outcome);
        Assert.Equal(first.TaskCompletionId,replay.TaskCompletionId);
        var row=await db.TaskCompletions.AsNoTracking().SingleAsync();
        Assert.Null(row.NutritionPlanId); Assert.Null(row.NutritionPlanDayNumber);
        Assert.All(await db.NutritionIntakeLogs.AsNoTracking().ToListAsync(),r=> {
            Assert.Null(r.NutritionPlanId); Assert.Null(r.NutritionPlanDayNumber);
        });
    }

    [NutritionPostgresFact]
    public async Task Mission_LocalMidnightSwitchesCycles_RangeAgreesAndFallsBackAfterLastCycle()
    {
        await using var db=Context(); var seed=await MissionSeedAsync(db,true);
        var c2=InboxPlan(seed.Patient,InboxNow.AddDays(1)); PublishInboxPlan(c2,MissionToday.AddDays(30));
        c2.Meals=seed.Prescription.Meals.Select(m=>new NutritionPrescriptionMeal {
            DayNumber=m.DayNumber,MealType=m.MealType,ScheduledTime=m.ScheduledTime,MenuName=$"Siguiente {m.DayNumber}",
            Items=m.Items.Select(i=>new NutritionPrescriptionItem { FoodItemId=i.FoodItemId,Grams=i.Grams,SortOrder=i.SortOrder }).ToList() }).ToList();
        db.NutritionPrescriptions.Add(c2); await db.SaveChangesAsync();
        var clock=new NutritionClock(DateTimeOffset.Parse("2026-11-06T04:59:59Z"));
        var before=NutritionStateMachine.Today(clock.GetUtcNow(),"America/Bogota");
        clock.Now=DateTimeOffset.Parse("2026-11-06T05:00:00Z");
        var after=NutritionStateMachine.Today(clock.GetUtcNow(),"America/Bogota");
        var resolver=MissionResolver(db);
        var range=await resolver.ResolveRangeAsync(seed.Patient,before,after,CancellationToken.None);
        Assert.Equal(seed.Prescription.Id,range[before].SourcePrescriptionId); Assert.Equal(30,range[before].PrescriptionDayNumber);
        Assert.Equal(c2.Id,range[after].SourcePrescriptionId); Assert.Equal(1,range[after].PrescriptionDayNumber);
        Assert.Equal(range[before],await resolver.ResolveAsync(seed.Patient,before,CancellationToken.None));
        Assert.Equal(range[after],await resolver.ResolveAsync(seed.Patient,after,CancellationToken.None));
        var next=Assert.Single((await MissionRepository(db).GetSnapshotAsync(seed.Enrollment,after))!.TodayTasks).Content!;
        Assert.Equal(1,next.NutritionPlanDayNumber); Assert.All(next.NutritionMeals!,m=>Assert.Equal("Siguiente 1",m.Description));
        // Sin publicación, se conserva exactamente el camino de bienestar existente.
        await db.NutritionPrescriptions.ExecuteUpdateAsync(s=>s.SetProperty(p=>p.IsPublished,false));
        var fallback=await resolver.ResolveAsync(seed.Patient,before,CancellationToken.None);
        Assert.Equal(seed.Wellness,fallback!.NutritionPlanId); Assert.Equal(1,fallback.NutritionPlanDayNumber);
        Assert.Null(fallback.SourcePrescriptionId); Assert.Null(fallback.PrescriptionDayNumber);
        var content=Assert.Single((await MissionRepository(db).GetSnapshotAsync(seed.Enrollment,before))!.TodayTasks).Content!;
        Assert.Equal(seed.Wellness,content.NutritionPlanId); Assert.Equal("Comida wellness",Assert.Single(content.NutritionMeals!).Description);
    }
}
