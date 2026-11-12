using CoppAddresd.Domain.Entities.WhatsApp;
using CoppAddresd.WhatsApp.Binding;

namespace CoppAddresd.WhatsApp.Tests;

[Trait("Category", "Unit")]
public sealed class BindingPolicyTests
{
    [Fact]
    public void DocumentQuery_DoesNotExposeDocumentToGenericLogging()
    {
        var query = new CoppAddresd.Application.Features.Patients.FindActivePatientForBindingQuery("1234567890");
        Assert.DoesNotContain("1234567890", query.ToString());
        Assert.DoesNotContain("1234567890", System.Text.Json.JsonSerializer.Serialize(query));
    }

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-12T18:00:00Z");

    [Fact]
    public void Session_ExpiresExactlyAtTenMinutes()
    {
        var session = new BindingSession();
        session.Begin(Now);
        Assert.False(session.IsExpired(Now.AddMinutes(10).AddTicks(-1)));
        Assert.True(session.IsExpired(Now.AddMinutes(10)));
    }

    [Fact]
    public void SendLimit_IsRollingHour_AndSurvivesRestart()
    {
        var session = new BindingSession();
        session.Begin(Now);
        session.ReserveSend(Now);
        session.ReserveSend(Now.AddMinutes(5));
        Assert.False(session.CanSend(Now.AddMinutes(59)));
        session.Begin(Now.AddMinutes(7));
        Assert.False(session.CanSend(Now.AddMinutes(7)));
        Assert.Throws<InvalidOperationException>(() => session.ReserveSend(Now.AddMinutes(8)));
        Assert.True(session.CanSend(Now.AddHours(1)));
        session.ReserveSend(Now.AddHours(1));
        Assert.False(session.CanSend(Now.AddHours(1).AddMinutes(1)));
        Assert.Equal(2, session.OtpSends);
    }

    [Theory]
    [InlineData(0, true)] [InlineData(2, true)] [InlineData(3, false)]
    public void Verification_HasThreeAttempts(int attempts, bool expected)
    {
        var session = new BindingSession { State = BindingState.AwaitingCode, Attempts = attempts, ExpiresAt = Now.AddMinutes(10) };
        Assert.Equal(expected, session.CanVerify(Now));
        Assert.False(session.CanVerify(Now.AddMinutes(10)));
    }

    [Theory]
    [InlineData("http://localhost:5123", true)]
    [InlineData("https://auth.example.test/", true)]
    [InlineData("http://user:secret@auth.example.test", false)]
    [InlineData("https://auth.example.test/?token=secret", false)]
    [InlineData("file:///auth", false)]
    public void AuthBaseUrl_RejectsEmbeddedCredentials(string url, bool valid)
    {
        var settings = new WhatsAppAuthOptions { BaseUrl = url };
        if (valid) Assert.NotNull(settings.GetBaseUri());
        else Assert.Throws<InvalidOperationException>(() => settings.GetBaseUri());
    }
}
