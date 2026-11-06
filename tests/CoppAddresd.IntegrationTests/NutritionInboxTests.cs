using System.Data.Common;
using System.Text.Json;
using CoppAddresd.Application.Features.Nutrition.Cases;
using CoppAddresd.Application.Features.Nutrition.Engine;
using CoppAddresd.Application.Features.Nutrition.Queries;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Persistence;
using CoppAddresd.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace CoppAddresd.IntegrationTests;

public sealed partial class NutritionPlanWorkflowTests
{
    private static readonly DateTime InboxNow = new(2026,11,6,2,0,0,DateTimeKind.Utc);
    private static NutritionActor InboxGlobal => new(Guid.NewGuid(),null,true,true,false);
    private NutritionInboxQueryHandler Inbox(AppDbContext db, DateTime? now = null)
        => new(new NutritionInboxRepository(db),new NutritionClock(new DateTimeOffset(now ?? InboxNow)));

    private async Task<PatientProfile> InboxPatient(AppDbContext db, string first, string last = "Ruiz")
    {
        var p = await PatientAsync(db); p.FirstName = first; p.LastName = last;
        await db.SaveChangesAsync(); return p;
    }
    private static NutritionPrescription InboxPlan(Guid patientId, DateTime created, Guid? id = null)
    {
        var p = new NutritionPatient(68.5,161,38,"F",26.4,Hba1c:6.5,NutRiesgo:"alto",SleepRF:2,RiesgoNum:4);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        return new() { Id=id ?? Guid.NewGuid(),PatientId=patientId,CreatedBy=Guid.NewGuid(),CreatedAt=created,
            ClinicalProfileJson=JsonSerializer.Serialize(p,json),TargetsJson=JsonSerializer.Serialize(NutritionEngine.ComputeTargets(p),json),
            CalorieTarget=1850,Provisional=true };
    }
    private static void PublishInboxPlan(NutritionPrescription plan, DateOnly end)
    {
        plan.State="aprobado"; plan.IsPublished=true; plan.StartDate=end.AddDays(-29); plan.EndDate=end;
        plan.ApprovedBy=plan.CreatedBy; plan.ApprovedAt=InboxNow;
    }

    [NutritionPostgresFact]
    public async Task Inbox_CurrentPublicationIsIndependentOfFutureCycle_AndChangesAtLocalMidnight()
    {
        await using var db=Context(); var p=await InboxPatient(db,"Ciclos"); p.Timezone="America/Bogota";
        var c1=InboxPlan(p.Id,InboxNow.AddDays(-2)); PublishInboxPlan(c1,new(2026,11,5));
        var c2=InboxPlan(p.Id,InboxNow.AddDays(-1)); c2.StartDate=new(2026,11,6); c2.EndDate=new(2026,12,5);
        c2.State="en_aprobacion";
        db.NutritionPrescriptions.AddRange(c1,c2); await db.SaveChangesAsync();
        var before=Assert.Single((await Inbox(db).Handle(new(InboxGlobal),CancellationToken.None)).Items);
        Assert.Equal(c2.Id,before.Prescription!.Id); Assert.True(before.HasPublishedToday);
        Assert.Equal(c1.StartDate,before.PublishedCycleStart); Assert.Equal(c1.EndDate,before.PublishedCycleEnd);
        Assert.Equal("en_aprobacion",before.LatestDraftState); Assert.True(before.HasFutureDraft);
        var after=Assert.Single((await Inbox(db,new DateTime(2026,11,6,5,0,0,DateTimeKind.Utc))
            .Handle(new(InboxGlobal),CancellationToken.None)).Items);
        Assert.False(after.HasPublishedToday); Assert.Null(after.PublishedCycleStart); Assert.Null(after.PublishedCycleEnd);
        Assert.False(after.HasFutureDraft); Assert.Equal("en_aprobacion",after.LatestDraftState);
    }

    [NutritionPostgresFact]
    public async Task Inbox_OnlyFutureDraft_AndAnyFutureDraftAreVisible_WithStablePaging()
    {
        await using var db=Context(); var p=await InboxPatient(db,"Futuro"); var q=await InboxPatient(db,"Otro");
        var future=InboxPlan(p.Id,InboxNow.AddDays(-1)); future.StartDate=new(2026,12,1); future.EndDate=new(2026,12,30);
        var latest=InboxPlan(p.Id,InboxNow); latest.State="devuelto";
        var other=InboxPlan(q.Id,InboxNow); other.StartDate=new(2026,12,1); other.EndDate=new(2026,12,30);
        db.NutritionPrescriptions.AddRange(future,latest,other); await db.SaveChangesAsync();
        var first=await Inbox(db).Handle(new(InboxGlobal,PageSize:1),CancellationToken.None);
        var second=await Inbox(db).Handle(new(InboxGlobal,Page:2,PageSize:1),CancellationToken.None);
        Assert.Equal(2,first.Total); Assert.Equal(2,second.Total);
        var rows=first.Items.Concat(second.Items).ToArray(); Assert.Equal(2,rows.Select(x=>x.PatientId).Distinct().Count());
        Assert.All(rows,x=> { Assert.False(x.HasPublishedToday); Assert.True(x.HasFutureDraft); Assert.Null(x.PublishedCycleStart); });
        Assert.Equal("devuelto",rows.Single(x=>x.PatientId==p.Id).LatestDraftState);
        Assert.Equal(first.Items[0].PatientId,(await Inbox(db).Handle(new(InboxGlobal,PageSize:1),CancellationToken.None)).Items[0].PatientId);
    }

    [NutritionPostgresFact]
    public async Task Inbox_ScopeLatestDraftFiltersNameAndState_IncludePatientsWithoutPlan()
    {
        await using var db = Context(); var ana = await InboxPatient(db,"Ana"); var beto = await InboxPatient(db,"Beto");
        var foreign = await InboxPatient(db,"Carla"); var inactive = await InboxPatient(db,"Dora"); var deleted = await InboxPatient(db,"Eva");
        deleted.DeletedAt=InboxNow;
        var professional = NutritionProfessional(ana.Id); db.Professionals.Add(professional);
        db.PatientProfessionalAssignments.AddRange(new PatientProfessionalAssignment { PatientId=ana.Id,Professional=professional },
            new() { PatientId=beto.Id,Professional=professional }, new() { PatientId=inactive.Id,Professional=professional,Status="Inactive" });
        var old = InboxPlan(beto.Id,InboxNow.AddDays(-2)); PublishInboxPlan(old,new(2026,12,4));
        var latest = InboxPlan(beto.Id,InboxNow.AddDays(-1)); latest.Version=2; latest.Revision=7;
        db.NutritionPrescriptions.AddRange(old,latest); await db.SaveChangesAsync();
        // Una fila legacy no se confunde con una prescripción del nuevo módulo.
        await DraftAsync(db,ana.Id);
        var actor = new NutritionActor(Guid.NewGuid(),professional.Id,false,true,false);
        var page = await Inbox(db).Handle(new(actor),CancellationToken.None);
        Assert.Equal(2,page.Total); Assert.Equal(new[]{ana.Id,beto.Id},page.Items.Select(x => x.PatientId));
        Assert.Equal("sin_plan",page.Items[0].State); Assert.Null(page.Items[0].Prescription);
        var prescription = page.Items[1].Prescription!;
        Assert.Equal(latest.Id,prescription.Id); Assert.Equal(2,prescription.Version); Assert.Equal(7,prescription.Revision);
        Assert.Equal("borrador_ia",prescription.State); Assert.True(prescription.Provisional); Assert.False(prescription.IsPublished);
        Assert.Equal(1850,prescription.K); Assert.Equal(4,prescription.CriticalAlertCount);
        Assert.Null(prescription.StartDate); Assert.Null(prescription.EndDate);
        var global = await Inbox(db).Handle(new(InboxGlobal),CancellationToken.None);
        Assert.Equal(4,global.Total); Assert.DoesNotContain(global.Items,x => x.PatientId==deleted.Id);
        Assert.Contains(global.Items,x => x.PatientId==foreign.Id); Assert.Contains(global.Items,x => x.PatientId==inactive.Id);
        Assert.Equal(beto.Id,Assert.Single((await Inbox(db).Handle(new(actor,State:"borrador_ia",Search:"bEtO rUiZ"),CancellationToken.None)).Items).PatientId);
        Assert.Empty((await Inbox(db).Handle(new(actor,State:"aprobado"),CancellationToken.None)).Items);
        Assert.Equal(ana.Id,Assert.Single((await Inbox(db).Handle(new(actor,State:"sin_plan",Search:"ANA"),CancellationToken.None)).Items).PatientId);
        Assert.Equal(0,(await Inbox(db).Handle(new(actor,State:"sin_plan",Search:"Beto"),CancellationToken.None)).Total);
        var reviewer = actor with { CanManage=false,CanApprove=true };
        Assert.Equal(2,(await Inbox(db).Handle(new(reviewer),CancellationToken.None)).Total);
        Assert.Equal(0,(await Inbox(db).Handle(new(actor with { ProfessionalId=Guid.NewGuid() }),CancellationToken.None)).Total);
        Assert.Equal(0,(await Inbox(db).Handle(new(actor with { ProfessionalId=null }),CancellationToken.None)).Total);
        await db.PatientProfessionalAssignments.Where(x => x.ProfessionalId==professional.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status,"Inactive"));
        Assert.Equal(0,(await Inbox(db).Handle(new(actor),CancellationToken.None)).Total);
    }

    [NutritionPostgresFact]
    public async Task Inbox_RenewalFilter_UsesEachPatientLocalDateBeforeCountingAndPaging()
    {
        await using var db = Context(); var bogota = await InboxPatient(db,"Bogota"); var utc = await InboxPatient(db,"Utc");
        bogota.Timezone="America/Bogota"; utc.Timezone="UTC";
        var a = InboxPlan(bogota.Id,InboxNow); var b = InboxPlan(utc.Id,InboxNow);
        PublishInboxPlan(a,new(2026,11,11)); PublishInboxPlan(b,new(2026,11,11));
        db.NutritionPrescriptions.AddRange(a,b); await db.SaveChangesAsync();
        var actor = InboxGlobal;
        var renewal = await Inbox(db).Handle(new(actor,PageSize:1,State:"por_renovar"),CancellationToken.None);
        Assert.Equal(1,renewal.Total); Assert.Equal(utc.Id,Assert.Single(renewal.Items).PatientId);
        Assert.Equal("por_renovar",renewal.Items[0].Prescription!.State);
        Assert.Equal(bogota.Id,Assert.Single((await Inbox(db).Handle(new(actor,State:"aprobado"),CancellationToken.None)).Items).PatientId);
        var empty = await Inbox(db).Handle(new(actor,Page:2,PageSize:1,State:"por_renovar"),CancellationToken.None);
        Assert.Empty(empty.Items); Assert.Equal(1,empty.Total);
        var expired = await Inbox(db,InboxNow.AddDays(10)).Handle(new(actor,State:"por_renovar"),CancellationToken.None);
        Assert.Equal(2,expired.Total);
        bogota.Timezone="Invalid/Zone"; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnprocessableEntityException>(() => Inbox(db).Handle(new(actor),CancellationToken.None));
    }

    [NutritionPostgresFact]
    public async Task Inbox_StableTiesLastAndEmptyPage_LiteralSearchAndCancellation()
    {
        await using var db = Context(); var a = await InboxPatient(db,"Ana"); var a2 = await InboxPatient(db,"Ana");
        var z = await InboxPatient(db,"Zoe"); var planPatient = await InboxPatient(db,"Plan");
        var olderPatient = await InboxPatient(db,"Anterior"); var literal = await InboxPatient(db,"Literal %_ \\");
        var low = InboxPlan(planPatient.Id,InboxNow,Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var high = InboxPlan(planPatient.Id,InboxNow,Guid.Parse("20000000-0000-0000-0000-000000000001")); high.State="devuelto";
        db.NutritionPrescriptions.AddRange(low,high,InboxPlan(olderPatient.Id,InboxNow.AddDays(-1))); await db.SaveChangesAsync();
        var actor = InboxGlobal; var ids = new List<Guid>();
        for (var page=1;page<=3;page++)
        {
            var result = await Inbox(db).Handle(new(actor,page,2),CancellationToken.None);
            Assert.Equal(6,result.Total); ids.AddRange(result.Items.Select(x => x.PatientId));
        }
        var expected = new[]{a.Id,a2.Id}.Order().Concat(new[]{literal.Id,z.Id,planPatient.Id,olderPatient.Id}).ToArray();
        Assert.Equal(expected,ids); Assert.Equal(6,ids.Distinct().Count());
        Assert.Equal(high.Id,Assert.Single((await Inbox(db).Handle(new(actor,Search:"Plan"),CancellationToken.None)).Items).Prescription!.Id);
        Assert.Equal(literal.Id,Assert.Single((await Inbox(db).Handle(new(actor,Search:"%_ \\"),CancellationToken.None)).Items).PatientId);
        var empty = await Inbox(db).Handle(new(actor,Page:4,PageSize:2),CancellationToken.None);
        Assert.Empty(empty.Items); Assert.Equal(6,empty.Total);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new NutritionInboxRepository(db)
            .ListAsync(actor,1,20,null,null,InboxNow,cancel.Token));
    }

    private sealed class InboxCommandCapture : DbCommandInterceptor
    {
        public List<(string Sql,NpgsqlParameter[] Parameters)> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken ct=default)
        {
            Commands.Add((command.CommandText,command.Parameters.Cast<NpgsqlParameter>()
                .Select(p => new NpgsqlParameter(p.ParameterName,p.NpgsqlDbType) { Value=p.Value }).ToArray()));
            return ValueTask.FromResult(result);
        }
    }

    [NutritionPostgresFact]
    public async Task Inbox_ThousandsOfHeaders_UsesTwoQueriesWithoutLoadingMeals_AndExplainsRealSql()
    {
        await using (var seed = Context())
        {
            var patients = Enumerable.Range(0,2000).Select(i => new PatientProfile {
                Id=Guid.NewGuid(),FirstName=$"Paciente {i:0000}",LastName="Prueba",Status="Activo" }).ToArray();
            seed.PatientProfiles.AddRange(patients); await seed.SaveChangesAsync();
            seed.NutritionPrescriptions.AddRange(patients.SelectMany(p => new[]{InboxPlan(p.Id,InboxNow.AddDays(-1)),InboxPlan(p.Id,InboxNow)}));
            await seed.SaveChangesAsync();
            await seed.Database.ExecuteSqlRawAsync("ANALYZE app.patient_profiles; ANALYZE app.nutrition_prescriptions;");
        }
        var capture = new InboxCommandCapture();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection).AddInterceptors(capture).Options);
        var result = await Inbox(db).Handle(new(InboxGlobal,PageSize:20),CancellationToken.None);
        Assert.Equal(2000,result.Total); Assert.Equal(20,result.Items.Count); Assert.Equal(2,capture.Commands.Count);
        Assert.Empty(db.ChangeTracker.Entries());
        await using var sql = new NpgsqlConnection(connection); await sql.OpenAsync();
        var query = capture.Commands.Last();
        await using var explain = new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) " + query.Sql,sql);
        explain.Parameters.AddRange(query.Parameters);
        var plan = (string)(await explain.ExecuteScalarAsync())!;
        using var json = JsonDocument.Parse(plan);
        Assert.Equal(20d,json.RootElement[0].GetProperty("Plan").GetProperty("Actual Rows").GetDouble());
        if (Environment.GetEnvironmentVariable("COP_NUTRITION_EXPLAIN_PATH") is { Length:>0 } path)
            await File.WriteAllTextAsync(path,plan);
    }
}
