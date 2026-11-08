using CoppAddresd.Api.Internal;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.Agents;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.Agents;

public sealed class GetPatientAiContextEndpointTests
{
    [Theory]
    [InlineData("test-key", "test-key", true)]
    [InlineData("test-key", null, false)]
    [InlineData("test-key", "wrong", false)]
    [InlineData("", "", false)]
    public void Autorizacion_ExigeClaveAiConfigurada(string expected, string? provided, bool allowed)
    {
        using var services = new ServiceCollection().AddSingleton(Options.Create(new AiServiceSettings { InternalApiKey = expected }))
            .BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        if (provided is not null) http.Request.Headers["X-Internal-Key"] = provided;
        var context = new AuthorizationFilterContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), []);
        new RequireAiInternalKeyAttribute().OnAuthorization(context);
        if (allowed) Assert.Null(context.Result);
        else Assert.IsType<UnauthorizedObjectResult>(context.Result);
    }

    [Fact]
    public async Task GetPatientContext_SinPaciente_Devuelve404()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<GetPatientAiContextQuery>(), Arg.Any<CancellationToken>()).Returns((PatientAiContextDto?)null);
        var response = await new InternalAgentsController(sender).GetPatientContext(Guid.NewGuid(), CancellationToken.None);
        Assert.IsType<NotFoundResult>(response.Result);
    }
}
