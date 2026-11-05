using CoppAddresd.Application.Features.Wellness;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.IntegrationTests;

public sealed partial class NutritionPlanWorkflowTests
{
    [NutritionPostgresFact]
    public async Task LegacyUpdate_FailureAfterDeletingDays_RollsBackPlanDaysAndAudit()
    {
        Guid id; Guid originalDay; int originalEvents;
        await using (var db = Context())
        {
            var patient = await PatientAsync(db); var plan = await DraftAsync(db, patient.Id);
            id = plan.Id; originalDay = plan.Days.First().Id;
            originalEvents = await db.NutritionPlanReviewEvents.CountAsync();
        }
        await using (var db = Context())
        {
            var handler = new UpdateNutritionPlanCommandHandler(new WellnessRepository(db), NullLogger<UpdateNutritionPlanCommandHandler>.Instance);
            // FK inexistente: PostgreSQL rechaza el insert después del borrado de los días.
            var invalid = new CreateNutritionPlanDayRequest(1, MealType.Desayuno, null, "Avena", 400,
                22, 40, 10, 8, 250, null, 1, Guid.NewGuid());
            var request = new UpdateNutritionPlanRequest("Cambio que debe revertir", null, null, 30, 1500,
                100, 150, 50, 30, "leche", "07:00", NutritionPlanStatus.Draft, [invalid], 1);
            await Assert.ThrowsAsync<DbUpdateException>(() => handler.Handle(new(id, request), CancellationToken.None));
        }
        await using var verify = Context();
        var preserved = await new WellnessRepository(verify).GetPlanByIdAsync(id, CancellationToken.None);
        Assert.Equal("Menú semanal revisable", preserved!.Name); Assert.Equal(1, preserved.Revision);
        Assert.Equal(28, preserved.Days.Count); Assert.Contains(preserved.Days, x => x.Id == originalDay);
        Assert.Equal(originalEvents, await verify.NutritionPlanReviewEvents.CountAsync());
    }

    [NutritionPostgresFact]
    public async Task PatientScope_ActiveAssignmentOnly_AndBothAssignmentListsFilterInDatabase()
    {
        await using var db = Context(); var own = await PatientAsync(db); var foreign = await PatientAsync(db);
        var organization = new Organization { Id = Guid.NewGuid(), Name = "Clínica de prueba", Code = Guid.NewGuid().ToString("N") };
        var professional = new Professional { Id = Guid.NewGuid(), Employee = new Employee { Id = Guid.NewGuid(),
            Organization = organization, FirstName = "Nutricionista", LastName = "Prueba", Email = "scope@example.invalid" } };
        db.Professionals.Add(professional);
        db.PatientProfessionalAssignments.Add(new() { PatientId = own.Id, ProfessionalId = professional.Id });
        db.PatientProfessionalAssignments.Add(new() { PatientId = foreign.Id, ProfessionalId = professional.Id, Status = "Inactive" });
        var plan = await DraftAsync(db, own.Id); var other = await DraftAsync(db, foreign.Id);
        var routine = new ExerciseRoutine { Id = Guid.NewGuid(), Name = "Rutina" }; db.ExerciseRoutines.Add(routine);
        db.NutritionPlanAssignments.AddRange(new NutritionPlanAssignment { Id = Guid.NewGuid(), PatientId = own.Id, PlanId = plan.Id, StartDate = DateTime.UtcNow },
            new NutritionPlanAssignment { Id = Guid.NewGuid(), PatientId = foreign.Id, PlanId = other.Id, StartDate = DateTime.UtcNow });
        db.RoutineAssignments.AddRange(new RoutineAssignment { Id = Guid.NewGuid(), PatientId = own.Id, RoutineId = routine.Id, StartDate = DateTime.UtcNow },
            new RoutineAssignment { Id = Guid.NewGuid(), PatientId = foreign.Id, RoutineId = routine.Id, StartDate = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var workflow = Workflow(db); var actor = new CoppAddresd.Application.Interfaces.NutritionReviewActor(Guid.NewGuid(), professional.Id, false);
        Assert.True(await workflow.CanAccessPatientAsync(own.Id, actor, CancellationToken.None));
        Assert.False(await workflow.CanAccessPatientAsync(foreign.Id, actor, CancellationToken.None));
        Assert.True(await workflow.CanAccessPatientAsync(foreign.Id, actor with { ViewAll = true }, CancellationToken.None));
        var repository = new WellnessRepository(db);
        var plans = await repository.ListPlansAsync(null, null, null, null, 1, 20, professional.Id, CancellationToken.None);
        Assert.Equal(own.Id, Assert.Single(plans.Items).PatientId);
        var nutrition = await repository.ListPlanAssignmentsAsync(null, null, null, 1, 20, professional.Id, CancellationToken.None);
        Assert.Equal(own.Id, Assert.Single(nutrition.Items).PatientId); Assert.Equal(1, nutrition.Total);
        var exercises = await repository.ListAssignmentsAsync(null, null, null, 1, 20, professional.Id, CancellationToken.None);
        Assert.Equal(own.Id, Assert.Single(exercises.Items).PatientId); Assert.Equal(1, exercises.Total);
    }

}
