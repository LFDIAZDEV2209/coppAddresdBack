using CoppAddresd.Auth.Services;
using CoppAddresd.Auth.Models;
using System.Text.Json;

namespace CoppAddresd.UnitTests;

public sealed class AuthOtpMessagesTests
{
    [Theory]
    [InlineData(null, "es")]
    [InlineData("", "es")]
    [InlineData("fr", "es")]
    [InlineData("es-CO", "es")]
    [InlineData("en", "en")]
    [InlineData(" EN-us ", "en")]
    public void Normalize_UsesSupportedLanguageOrSpanishForLegacyClients(string? requested, string expected)
        => Assert.Equal(expected, AuthMessageLanguage.Normalize(requested));

    [Fact]
    public void MobileRequestJson_BindsLanguageForBothSendContractsAndPreservesLegacyFallback()
    {
        var json = """{"documentNumber":"QA-ID","contactId":"email","application":"app","language":"en"}""";
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var activation = JsonSerializer.Deserialize<SendOtpRequest>(json, options)!;
        var recovery = JsonSerializer.Deserialize<PasswordRecoveryCodeRequest>(json, options)!;
        Assert.Equal("en", activation.Language);
        Assert.Equal("en", recovery.Language);
        Assert.Equal("email", activation.ContactId);
        Assert.Equal("app", recovery.Application);
        var legacy = JsonSerializer.Deserialize<SendOtpRequest>("""{"documentNumber":"QA-ID","contactId":"phone"}""", options)!;
        Assert.Equal("es", AuthMessageLanguage.Normalize(legacy.Language));
    }

    [Fact]
    public void Activation_EncodesHtmlNameAndPreservesItsPlainTextValue()
    {
        const string name = "Ana <img src=x onerror=alert(1)>";
        var message = AuthOtpMessages.Activation("qa@example.invalid", name, "123456", 300, "en");
        Assert.Contains("Ana &lt;img", message.HtmlBody);
        Assert.DoesNotContain("<img", message.HtmlBody);
        Assert.Contains(name, message.PlainTextBody);
        Assert.Contains("123456", message.HtmlBody);
        Assert.Contains("5 minutes", message.PlainTextBody);
    }
}
