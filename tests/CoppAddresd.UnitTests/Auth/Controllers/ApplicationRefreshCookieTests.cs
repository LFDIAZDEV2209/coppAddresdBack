using CoppAddresd.Auth.Controllers;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Moq;
namespace CoppAddresd.UnitTests.Auth.Controllers;
public sealed class ApplicationRefreshCookieTests
{
    [Theory]
    [InlineData("erp")] [InlineData("app")]
    public async Task Refresh_ReadsAndRotatesOnlyRequestedApplicationCookie(string application)
    {
        var auth = new Mock<IAuthService>(); var env = new Mock<IHostEnvironment>(); env.SetupGet(e => e.EnvironmentName).Returns("Production");
        auth.Setup(a => a.RefreshAsync(application + "-token", default, application)).ReturnsAsync(new TokenResult("access", "rotated", "Bearer", 300));
        var controller = new AuthController(auth.Object, Mock.Of<IOtpService>(), env.Object) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        controller.Request.Headers.Cookie = "copp_refresh_token_erp=erp-token; copp_refresh_token_app=app-token";
        var result = await controller.Refresh(new RefreshTokenRequest { Application = application }, default);
        Assert.IsType<OkObjectResult>(result.Result);
        var cookies = string.Join(";", controller.Response.Headers.SetCookie.ToArray());
        Assert.Contains($"copp_refresh_token_{application}=rotated", cookies);
        Assert.DoesNotContain($"copp_refresh_token_{(application == "erp" ? "app" : "erp")}=", cookies);
        Assert.Contains("httponly", cookies.ToLowerInvariant()); Assert.Contains("secure", cookies.ToLowerInvariant());
    }
    [Fact]
    public async Task FailedConcurrentRefreshDoesNotDeleteNewlyRotatedCookie()
    {
        var controller = new AuthController(Mock.Of<IAuthService>(), Mock.Of<IOtpService>(), Mock.Of<IHostEnvironment>()) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        controller.Request.Headers.Cookie = "copp_refresh_token_erp=old";
        await controller.Refresh(new RefreshTokenRequest { Application = "erp" }, default);
        Assert.Equal(0, controller.Response.Headers.SetCookie.Count);
        Assert.Equal("invalid", controller.Response.Headers["X-Refresh-Status"]);
    }
}
