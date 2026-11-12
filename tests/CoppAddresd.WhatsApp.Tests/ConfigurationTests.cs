using CoppAddresd.WhatsApp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoppAddresd.WhatsApp.Tests;

[Trait("Category", "Unit")]
public sealed class ConfigurationTests
{
    [Fact]
    public void Disabled_RegistersNothing_AndIgnoresAppsettings()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["WHATSAPP_ENABLED"] = "1", ["WhatsApp:AccessToken"] = "ignored" }).Build();
        services.AddWhatsApp(config, options: WhatsAppOptions.FromEnvironment(_ => null));
        Assert.Empty(services);
    }

    [Fact]
    public void Binding_UsesOnlyNamedEnvironmentVariables()
    {
        var options = WhatsAppOptions.FromEnvironment(key => key == "WHATSAPP_ENABLED" ? "1" : key);
        Assert.True(options.Enabled);
        Assert.Equal("WHATSAPP_ACCESS_TOKEN", options.AccessToken);
        Assert.Equal("WHATSAPP_APP_SECRET", options.AppSecret);
        Assert.Equal("WHATSAPP_APP_ID", options.AppId);
        Assert.Equal("WHATSAPP_PHONE_NUMBER_ID", options.PhoneNumberId);
        Assert.Equal("WHATSAPP_WEBHOOK_VERIFY_TOKEN", options.WebhookVerifyToken);
        options.Validate();
    }
}
