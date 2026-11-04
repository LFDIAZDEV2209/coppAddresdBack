using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.Sos;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Infrastructure.Services;
using CoppAddresd.Api.Controllers;
using CoppAddresd.Api.Security;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.Sos;

public sealed class SosLocationLinkTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private static SosLocationLinkService Service(string origin = "https://alerts.example.test", string? key = "test-only-location-signature-key-not-for-production") =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:SignatureKey"] = key,
        }).Build(), Options.Create(new SosWebhookSettings { BaseUrl = origin }));

    private static SosAlert Alert() => new()
    {
        Id = Guid.NewGuid(), Status = SosAlertStatus.Activa,
        PatientId = Guid.NewGuid(), DestinationPhoneE164 = "+573001234567",
        Latitude = 4.711, Longitude = -74.0721,
        CreatedAt = Now.UtcDateTime,
    };

    private static string Token(string link) => new Uri(link).Segments[^1];

    [Fact]
    public void CreateLink_ProducesOpaqueFirstPartyUrlWithoutCoordinatesOrPatientIdentity()
    {
        var service = Service();
        var alert = Alert();
        var link = service.CreateLink(alert)!;
        Assert.StartsWith("https://alerts.example.test/api/v1/sos/maps/", link);
        Assert.DoesNotContain("4.711", link);
        Assert.DoesNotContain("74.072", link);
        Assert.DoesNotContain(alert.Id.ToString(), link);
        Assert.Equal(43, Token(link).Length);
        Assert.True(service.TryReadToken(Token(link), out var id));
        Assert.Equal(alert.Id, id);
    }

    [Fact]
    public void Token_StableAcrossRestartsButWrongKeyOrTamperingIsRejected()
    {
        var alert = Alert();
        var token = Token(Service().CreateLink(alert)!);
        Assert.True(Service().TryReadToken(token, out var id));
        Assert.Equal(alert.Id, id);
        Assert.False(Service(key: "a-different-test-only-location-key-for-other-environment").TryReadToken(token, out _));
        var changed = (token[0] == 'A' ? 'B' : 'A') + token[1..];
        Assert.False(Service().TryReadToken(changed, out _));
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("bad")]
    [InlineData("javascript:alert(1)")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void Token_InvalidOrForgedInput_IsRejected(string? token) =>
        Assert.False(Service().TryReadToken(token, out _));

    [Theory]
    [InlineData("http://remote.example.test")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://user:password@example.test")]
    [InlineData("https://alerts.example.test?redirect=evil")]
    [InlineData("https://alerts.example.test/not-an-origin")]
    public void CreateLink_UntrustedOrInvalidPublicOrigin_IsUnavailable(string origin) =>
        Assert.Null(Service(origin).CreateLink(Alert()));

    [Fact]
    public void CreateLink_NoCoordinatesOrSigningConfiguration_IsUnavailable()
    {
        var alert = Alert();
        alert.Latitude = null;
        Assert.Null(Service().CreateLink(alert));
        Assert.Null(Service(key: null).CreateLink(Alert()));
        Assert.Null(Service(key: "too-short").CreateLink(Alert()));
    }

    [Fact]
    public async Task Resolve_ValidActiveAlert_RedirectsOnlyToGoogleMaps()
    {
        var alert = Alert();
        var service = Service();
        var repository = Substitute.For<ISosAlertRepository>();
        repository.GetByIdAsync(alert.Id, Arg.Any<CancellationToken>()).Returns(alert);
        var handler = new ResolveSosLocationLinkHandler(service, repository, new FixedClock());
        var url = await handler.Handle(new(Token(service.CreateLink(alert)!)), CancellationToken.None);
        Assert.Equal("https://www.google.com/maps/search/?api=1&query=4.711000,-74.072100", url);
    }

    [Fact]
    public async Task Resolve_CancelledAlert_RevokesLocationAccess()
    {
        var alert = Alert();
        var service = Service();
        var token = Token(service.CreateLink(alert)!);
        alert.Status = SosAlertStatus.Cancelada;
        var repository = Substitute.For<ISosAlertRepository>();
        repository.GetByIdAsync(alert.Id, Arg.Any<CancellationToken>()).Returns(alert);
        Assert.Null(await new ResolveSosLocationLinkHandler(service, repository, new FixedClock()).Handle(new(token), CancellationToken.None));
    }

    [Fact]
    public async Task Resolve_ClinicallyAttendedAlert_StillLetsContactSeeSharedLocationWithinLifetime()
    {
        var alert = Alert();
        alert.Status = SosAlertStatus.Atendida;
        var service = Service();
        var repository = Substitute.For<ISosAlertRepository>();
        repository.GetByIdAsync(alert.Id, Arg.Any<CancellationToken>()).Returns(alert);
        Assert.NotNull(await new ResolveSosLocationLinkHandler(service, repository, new FixedClock()).Handle(new(Token(service.CreateLink(alert)!)), CancellationToken.None));
    }

    [Theory]
    [InlineData(-24)] [InlineData(-48)] [InlineData(1)]
    public async Task Resolve_ExpiredOrFutureAlert_DoesNotRevealLocation(int hours)
    {
        var alert = Alert();
        alert.CreatedAt = Now.AddHours(hours).UtcDateTime;
        var service = Service();
        var repository = Substitute.For<ISosAlertRepository>();
        repository.GetByIdAsync(alert.Id, Arg.Any<CancellationToken>()).Returns(alert);
        Assert.Null(await new ResolveSosLocationLinkHandler(service, repository, new FixedClock()).Handle(new(Token(service.CreateLink(alert)!)), CancellationToken.None));
    }

    [Fact]
    public async Task Resolve_InvalidToken_DoesNotQueryPatientsOrAlerts()
    {
        var repository = Substitute.For<ISosAlertRepository>();
        var handler = new ResolveSosLocationLinkHandler(Service(), repository, new FixedClock());
        Assert.Null(await handler.Handle(new("invalid"), CancellationToken.None));
        await repository.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
    }

    [Fact]
    public async Task Resolve_CancelledRequest_StopsBeforeReadingPrivateLocation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var repository = Substitute.For<ISosAlertRepository>();
        var handler = new ResolveSosLocationLinkHandler(Service(), repository, new FixedClock());
        await Assert.ThrowsAsync<OperationCanceledException>(() => handler.Handle(new("invalid"), cts.Token));
        await repository.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
    }

    [Fact]
    public async Task ActiveAlert_ExposesActualEmailChannelStatusWithoutNewDeliveryClaims()
    {
        var alert = Alert();
        var repository = Substitute.For<ISosAlertRepository>();
        repository.GetActiveByPatientAsync(alert.PatientId, Arg.Any<CancellationToken>()).Returns(alert);
        var dedupe = Substitute.For<INotificationDedupeRepository>();
        dedupe.GetByKeyAsync($"sos:email:{alert.Id}", Arg.Any<CancellationToken>())
            .Returns(new NotificationDedupeKey { EmailStatus = "noconfigurado" });
        var result = await new GetActiveSosAlertHandler(repository, dedupe).Handle(new(alert.PatientId), CancellationToken.None);
        Assert.Equal("NoConfigurado", result?.EmailChannelStatus);
    }

    [Fact]
    public async Task PublicMap_Redirect_DoesNotCacheOrLeakSharedTokenInReferrer()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ResolveSosLocationLinkQuery>(), Arg.Any<CancellationToken>())
            .Returns("https://www.google.com/maps/search/?api=1&query=4.711000,-74.072100");
        var controller = new SosLocationLinksController(mediator)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        var result = Assert.IsType<RedirectResult>(await controller.Open("opaque-private-token", CancellationToken.None));
        Assert.StartsWith("https://www.google.com/maps/", result.Url);
        Assert.Equal("no-store, max-age=0", controller.Response.Headers.CacheControl.ToString());
        Assert.Equal("no-referrer", controller.Response.Headers["Referrer-Policy"].ToString());
        Assert.Equal("noindex, nofollow, noarchive", controller.Response.Headers["X-Robots-Tag"].ToString());
    }

    [Theory]
    [InlineData("/api/v1/sos/maps/private-token", "/api/v1/sos/maps/[redacted]")]
    [InlineData("/API/v1/SOS/maps/private-token", "/api/v1/sos/maps/[redacted]")]
    [InlineData("/api/v1/sos/alerts", "/api/v1/sos/alerts")]
    public void Logging_RedactsOnlySharedLocationCapabilities(string path, string expected) =>
        Assert.Equal(expected, RequestLogPath.Redact(new PathString(path)));

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
