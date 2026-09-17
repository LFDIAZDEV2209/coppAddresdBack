using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using CoppAddresd.Api.Context;
using CoppAddresd.Api.Controllers;
using CoppAddresd.Application.Features.ProgramProgress.Commands.RecordWeight;
using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace CoppAddresd.UnitTests.ProgramProgress;

public class WeightAuthorizationTests
{
    [Theory]
    [InlineData(null, 401)]
    [InlineData("Patient", 403)]
    [InlineData("ClinicAdmin", 403)]
    [InlineData("Admin", 201)]
    public async Task OnlyExistingGlobalAdminCanReachWeightWrite(string? role, int expectedStatus)
    {
        var key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));
        var actor = new Mock<IProgramActorContext>();
        actor.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        actor.Setup(x => x.ResolvePatientProfileIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
        actor.Setup(x => x.ResolveActiveEnrollmentIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
        var mediator = new Mock<IMediator>();
        mediator.Setup(x => x.Send(It.IsAny<RecordWeightCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecordedWeightDto(Guid.NewGuid(), 80, new DateOnly(2026, 9, 14), DateTime.UtcNow));
        using var server = new TestServer(new WebHostBuilder().ConfigureServices(services => {
            services.AddControllers().AddApplicationPart(typeof(ProgramController).Assembly);
            services.AddSingleton(actor.Object);
            services.AddSingleton(mediator.Object);
            services.AddSingleton(Mock.Of<IObjectStorageService>());
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => {
                options.TokenValidationParameters = new() { ValidateIssuer = false, ValidateAudience = false,
                    IssuerSigningKey = key, ValidateLifetime = true };
            });
            services.AddAuthorization();
        }).Configure(app => {
            app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }));
        using var client = server.CreateClient();
        if (role is not null) {
            // Incluso un permiso administrativo no convierte ClinicAdmin en Admin global.
            var token = new JwtSecurityToken(claims: [new Claim(ClaimTypes.Role, role),
                new Claim("permission", "System.AdminSettings")], expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new(key, SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        }
        var response = await client.PostAsJsonAsync("/api/v1/program/me/weight", new RecordWeightRequest(80, new DateOnly(2026, 9, 14)));
        Assert.Equal((HttpStatusCode)expectedStatus, response.StatusCode);
        mediator.Verify(x => x.Send(It.IsAny<RecordWeightCommand>(), It.IsAny<CancellationToken>()),
            role == "Admin" ? Times.Once() : Times.Never());
    }
}
