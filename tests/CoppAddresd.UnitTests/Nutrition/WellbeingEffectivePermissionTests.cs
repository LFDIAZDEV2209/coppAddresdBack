using System.Security.Claims;
using CoppAddresd.Api.Authorization;
using CoppAddresd.Api.Context;
using CoppAddresd.Api.Controllers;
using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace CoppAddresd.UnitTests.Nutrition;

public sealed class WellbeingEffectivePermissionTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task PermissionFilter_UsesEffectiveScopedPermission_WithoutGlobalClaim(bool allowed)
    {
        var actor = Substitute.For<ICurrentContext>();
        actor.HasPermissionAsync("Nutrition.View", Arg.Any<CancellationToken>()).Returns(allowed);
        using var services = new ServiceCollection().AddSingleton(actor).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("aud", "erp")], "test")) };
        var context = new AuthorizationFilterContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), []);
        await new RequireContextPermissionAttribute("Nutrition.View").OnAuthorizationAsync(context);
        if (allowed) Assert.Null(context.Result); else Assert.IsType<ForbidResult>(context.Result);
        await actor.Received(1).HasPermissionAsync("Nutrition.View", http.RequestAborted);
    }

    [Fact]
    public async Task PatientSelector_ScopedProfileCapability_AlwaysPassesProfessionalFilter()
    {
        var actor = Substitute.For<ICurrentContext>(); var professional = Guid.NewGuid();
        actor.HasPermissionAsync("Program.Profile360View", Arg.Any<CancellationToken>()).Returns(true);
        actor.HasPermissionAsync("Patients.View", Arg.Any<CancellationToken>()).Returns(true);
        actor.GetProfessionalIdAsync(Arg.Any<CancellationToken>()).Returns(professional);
        var mediator = Substitute.For<IMediator>();
        var controller = new PatientsController(mediator, actor, Substitute.For<IEmployeeRepository>())
        { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([], "test")) } } };
        await controller.List(ct: CancellationToken.None);
        await mediator.Received(1).Send(Arg.Is<ListPatientsQuery>(q => q.OwnProfessionalId == professional), CancellationToken.None);
        actor.GetProfessionalIdAsync(Arg.Any<CancellationToken>()).Returns((Guid?)null);
        Assert.IsType<ForbidResult>((await controller.List(ct: CancellationToken.None)).Result);
    }
}
