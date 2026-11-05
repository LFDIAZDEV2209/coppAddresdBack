using System.Security.Claims;
using CoppAddresd.Api.Authorization;
using CoppAddresd.Api.Constants;
using CoppAddresd.Api.Context;
using CoppAddresd.Api.Controllers;
using CoppAddresd.Application.Features.Wellness;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Nutrition;

public sealed class WellnessPatientScopeTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var route in new[] { "plan-get", "plan-create", "plan-update", "plan-delete", "clone-source", "clone-target",
            "plans-list", "nutrition-list", "nutrition-list-plan", "nutrition-get", "nutrition-patient", "nutrition-create", "nutrition-update",
            "nutrition-delete", "routine-list", "routine-get", "routine-patient", "routine-create", "routine-update", "routine-delete", "generate" })
            foreach (var scope in new[] { "own", "foreign", "global" }) yield return [route, scope];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task PatientRoutes_RespectOwnForeignAndGlobalScope(string route, string scope)
    {
        var patientId = Guid.NewGuid(); var id = Guid.NewGuid(); var professionalId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>(); var context = Substitute.For<ICurrentContext>();
        var workflow = Substitute.For<INutritionPlanWorkflow>();
        context.UserId.Returns(Guid.NewGuid()); context.GetProfessionalIdAsync(Arg.Any<CancellationToken>()).Returns(professionalId);
        workflow.CanAccessPatientAsync(patientId, Arg.Any<NutritionReviewActor>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<NutritionReviewActor>().ViewAll || scope == "own");
        var controller = new WellnessController(mediator, NullLogger<WellnessController>.Instance, context, workflow)
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
                scope == "global" ? [new Claim(PermissionClaimTypes.Permission, PermissionCodes.PatientsView)] : [], "test")) } }
        };
        var dto = NutritionPlanDto.FromEntity(new NutritionPlan { Id = id, Name = "Plan", PatientId = route == "clone-target" ? null : patientId });
        mediator.Send(Arg.Any<GetNutritionPlanQuery>(), Arg.Any<CancellationToken>()).Returns(dto);
        mediator.Send(Arg.Any<CreateNutritionPlanCommand>(), Arg.Any<CancellationToken>()).Returns(dto);
        mediator.Send(Arg.Any<UpdateNutritionPlanCommand>(), Arg.Any<CancellationToken>()).Returns(dto);
        mediator.Send(Arg.Any<CloneNutritionPlanCommand>(), Arg.Any<CancellationToken>()).Returns(dto);
        mediator.Send(Arg.Any<DeleteNutritionPlanCommand>(), Arg.Any<CancellationToken>()).Returns(true);
        var nutrition = NutritionPlanAssignmentDto.FromEntity(new() { Id = id, PatientId = patientId });
        mediator.Send(Arg.Any<GetNutritionPlanAssignmentQuery>(), Arg.Any<CancellationToken>()).Returns(nutrition);
        mediator.Send(Arg.Any<CreateNutritionPlanAssignmentCommand>(), Arg.Any<CancellationToken>()).Returns(nutrition);
        mediator.Send(Arg.Any<UpdateNutritionPlanAssignmentCommand>(), Arg.Any<CancellationToken>()).Returns(nutrition);
        mediator.Send(Arg.Any<DeleteNutritionPlanAssignmentCommand>(), Arg.Any<CancellationToken>()).Returns(true);
        var routine = RoutineAssignmentDto.FromEntity(new() { Id = id, PatientId = patientId });
        mediator.Send(Arg.Any<GetRoutineAssignmentQuery>(), Arg.Any<CancellationToken>()).Returns(routine);
        mediator.Send(Arg.Any<CreateRoutineAssignmentCommand>(), Arg.Any<CancellationToken>()).Returns(routine);
        mediator.Send(Arg.Any<UpdateRoutineAssignmentCommand>(), Arg.Any<CancellationToken>()).Returns(routine);
        mediator.Send(Arg.Any<DeleteRoutineAssignmentCommand>(), Arg.Any<CancellationToken>()).Returns(true);
        var create = new CreateNutritionPlanRequest("Plan", null, null, 30, 1800, null, null, null, null,
            null, null, false, patientId, null, NutritionPlanStatus.Draft, null);
        using var cancellation = new CancellationTokenSource(); var ct = cancellation.Token;
        IActionResult? result = route switch
        {
            "plan-get" => (await controller.GetNutritionPlan(id, ct)).Result,
            "plan-create" => (await controller.CreateNutritionPlan(create, ct)).Result,
            "plan-update" => (await controller.UpdateNutritionPlan(id, new("Plan", null, null, 30, 1800,
                null, null, null, null, null, null, NutritionPlanStatus.Draft, null), ct)).Result,
            "plan-delete" => await controller.DeleteNutritionPlan(id, ct),
            "clone-source" => (await controller.CloneNutritionPlan(id, null, ct)).Result,
            "clone-target" => (await controller.CloneNutritionPlan(id, patientId, ct)).Result,
            "plans-list" => (await controller.ListNutritionPlans(patientId: patientId, ct: ct)).Result,
            "nutrition-list" => (await controller.ListNutritionPlanAssignments(patientId: patientId, ct: ct)).Result,
            "nutrition-list-plan" => (await controller.ListNutritionPlanAssignments(planId: id, ct: ct)).Result,
            "nutrition-get" => (await controller.GetNutritionPlanAssignment(id, ct)).Result,
            "nutrition-patient" => (await controller.ListPlanAssignmentsByPatient(patientId, ct)).Result,
            "nutrition-create" => (await controller.CreateNutritionPlanAssignment(new(patientId, id, DateTime.UtcNow, null, AssignmentStatus.Active, null), ct)).Result,
            "nutrition-update" => (await controller.UpdateNutritionPlanAssignment(id, new(DateTime.UtcNow, null, AssignmentStatus.Active, null), ct)).Result,
            "nutrition-delete" => await controller.DeleteNutritionPlanAssignment(id, ct),
            "routine-list" => (await controller.ListRoutineAssignments(patientId: patientId, ct: ct)).Result,
            "routine-get" => (await controller.GetRoutineAssignment(id, ct)).Result,
            "routine-patient" => (await controller.ListAssignmentsByPatient(patientId, ct)).Result,
            "routine-create" => (await controller.CreateRoutineAssignment(new(patientId, id, DateTime.UtcNow, null, default, AssignmentStatus.Active, null), ct)).Result,
            "routine-update" => (await controller.UpdateRoutineAssignment(id, new(DateTime.UtcNow, null, default, AssignmentStatus.Active, null), ct)).Result,
            "routine-delete" => await controller.DeleteRoutineAssignment(id, ct),
            "generate" => (await controller.GeneratePlan(new(patientId, "nutrition"), ct)).Result,
            _ => throw new InvalidOperationException()
        };
        if (scope == "foreign") Assert.IsType<NotFoundResult>(result);
        else Assert.True(result is OkObjectResult or CreatedAtActionResult or NoContentResult, $"{route}: {result?.GetType().Name}");
        await workflow.Received().CanAccessPatientAsync(patientId,
            Arg.Is<NutritionReviewActor>(a => a.ViewAll == (scope == "global") && a.ProfessionalId == professionalId), ct);
    }
}
