using System.Security.Claims;
using CoppAddresd.Api.Authorization;
using CoppAddresd.Api.Controllers;
using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Api.Authorization;

public sealed class ErpDirectoryAudienceTests
{
    [Theory]
    [InlineData("Employees.View")]
    [InlineData("Employees.Update")]
    [InlineData("Organizations.View")]
    [InlineData("Clinics.Create")]
    [InlineData("Locations.Update")]
    [InlineData("Professionals.Create")]
    [InlineData("Professionals.Update")]
    [InlineData("Professionals.Delete")]
    public async Task PermisoAdministrativo_ExigeSesionErpAunqueAppTengaClaim(string code)
    {
        Assert.False(await Allowed(code, "app"));
        Assert.False(await Allowed(code, null));
        Assert.True(await Allowed(code, "erp"));
    }

    [Theory]
    [InlineData("Patients.View")]
    [InlineData("Program.View")]
    [InlineData("HealthTests.ViewOwn")]
    [InlineData("Professionals.View")]
    public async Task PermisoCompartido_ConservaSemanticaPaciente(string code) =>
        Assert.True(await Allowed(code, "app"));

    private static async Task<bool> Allowed(string code, string? audience)
    {
        var claims = new List<Claim> { new("permission", code) };
        if (audience is not null)
            claims.Add(new("aud", audience));
        var requirement = new PermissionRequirement(code);
        var context = new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(new ClaimsIdentity(claims, "test")),
            null
        );
        await new PermissionHandler(NullLogger<PermissionHandler>.Instance).HandleAsync(context);
        return context.HasSucceeded;
    }

    [Fact]
    public async Task EstadisticasDirectorio_AppEsRechazadaAntesDeConsultarDatos()
    {
        var mediator = Substitute.For<IMediator>();
        var controller = new ProfessionalsController(
            mediator,
            Substitute.For<IEmployeeRepository>(),
            Substitute.For<IAuthScopedAssignmentsClient>(),
            NullLogger<ProfessionalsController>.Instance
        )
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity([new Claim("aud", "app")], "test")
                    ),
                },
            },
        };
        Assert.IsType<ForbidResult>((await controller.Stats()).Result);
        Assert.Empty(mediator.ReceivedCalls());
    }
}
