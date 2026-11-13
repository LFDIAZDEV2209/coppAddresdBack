using System.Reflection;
using CoppAddresd.Api.Authorization;
using CoppAddresd.Api.Controllers;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Services;
using CoppAddresd.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace CoppAddresd.UnitTests.ProgramProgress.Api;

public sealed class PatientScopeRouteTests
{
    public static IEnumerable<object[]> Routes()
    {
        foreach (var type in new[] { typeof(ProgramController), typeof(WellnessController), typeof(ProgramControlsController),
            typeof(NutritionController), typeof(NutritionReadController), typeof(NutritionPlanReviewController) })
            foreach (var method in type.GetMethods())
                foreach (var attribute in method.GetCustomAttributes<PatientScopeAttribute>())
                    foreach (var scenario in new[] { "asignado", "ajeno", "Admin", "sin_permiso" })
                        yield return [type, method.Name, attribute.Parameter, scenario];
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Route_ChecksPermissionAndPatientBeforeExecuting(Type type, string methodName, string parameter, string scenario)
    {
        var method = type.GetMethod(methodName)!;
        var attribute = method.GetCustomAttributes<PatientScopeAttribute>().Single(x => x.Parameter == parameter);
        var path = parameter.Split('.');
        var parameterType = method.GetParameters().Single(p => p.Name == path[0]).ParameterType;
        if (path.Length > 1) Assert.NotNull(parameterType.GetProperty(path[1]));
        var patient = Guid.NewGuid();
        var actor = Substitute.For<IPatientScopeActor>();
        actor.UserId.Returns(Guid.NewGuid()); actor.IsAdmin.Returns(scenario == "Admin");
        actor.ProfessionalIdAsync(Arg.Any<CancellationToken>()).Returns(Guid.NewGuid());
        actor.HasPermissionAsync(attribute.Permission, Arg.Any<CancellationToken>()).Returns(scenario != "sin_permiso");
        var store = Substitute.For<IPatientScopeStore>();
        var visible = (scenario == "ajeno" ? Array.Empty<Guid>() : new[] { patient }).AsQueryable();
        store.VisiblePatients(Arg.Any<Guid?>(), Arg.Any<bool>()).Returns(visible);
        store.ContainsAsync(visible, patient, Arg.Any<CancellationToken>()).Returns(visible.Contains(patient));
        store.OwnerAsync(Arg.Any<PatientResource>(), patient, Arg.Any<CancellationToken>()).Returns(patient);
        using var services = new ServiceCollection().AddSingleton(store)
            .AddSingleton<IPatientScopeGuard>(new PatientScopeGuard(actor, store)).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        var action = new ActionContext(http, new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
        var args = new Dictionary<string, object?> { [path[0]] = path.Length == 1 ? patient : new Input(patient) };
        var context = new ActionExecutingContext(action, [], args, new object());
        var executed = false;
        Task<ActionExecutedContext> Next() { executed = true; return Task.FromResult(new ActionExecutedContext(action, [], context.Controller)); }
        if (scenario == "ajeno") await Assert.ThrowsAsync<NotFoundException>(() => attribute.OnActionExecutionAsync(context, Next));
        else if (scenario == "sin_permiso") await Assert.ThrowsAsync<ForbiddenException>(() => attribute.OnActionExecutionAsync(context, Next));
        else await attribute.OnActionExecutionAsync(context, Next);
        Assert.Equal(scenario is "asignado" or "Admin", executed);
    }

    [Fact]
    public void EveryProfileAndEnrollmentDetailRoute_DeclaresScope()
    {
        string[] methods = ["GetPatientOverview", "GetPatientControls", "GetEnrollmentSnapshot", "GetEnrollment", "GetXpLedger",
            "GetBaselines", "CreateBaseline", "GetProgramContent", "SetWeekContent", "SetWeekContentRange",
            "GetEnrollmentWeek", "ReplaceEnrollmentWeekTasks", "EnrollPatient", "PauseEnrollment", "ResumeEnrollment",
            "WithdrawEnrollment", "BulkAssignProgramContent", "CalculateScores", "GetAdaptation", "DecideAdaptation"];
        foreach (var method in methods)
            Assert.NotEmpty(typeof(ProgramController).GetMethod(method)!.GetCustomAttributes<PatientScopeAttribute>());
    }

    private sealed record Input(Guid PatientId)
    {
        public Guid TargetId => PatientId;
        public Guid EnrollmentId => PatientId;
        public Guid PlanId => PatientId;
        public Guid SourcePlanId => PatientId;
        public Guid NutritionPlanId => PatientId;
        public string TargetType => "Enrollment";
    }
}
