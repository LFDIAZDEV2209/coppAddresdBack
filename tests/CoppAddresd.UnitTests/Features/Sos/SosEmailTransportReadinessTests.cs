using CoppAddresd.Application.DTOs.Email;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Infrastructure.Services;
using CoppAddresd.Infrastructure.Services.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.Sos;

public sealed class SosEmailTransportReadinessTests
{
    private static EmailMessageDto PrivateMessage() => new()
    {
        To = ["private-person@example.test"],
        Subject = "Nombre privado Sofía Vega",
        Body = "Ubicación 4.711,-74.0721 y teléfono +573055550123",
    };

    [Fact]
    public async Task LogSender_NoPuedeEntregarCorreo_YNoRegistraPii()
    {
        var logger = new CapturedLogger<LogEmailService>();
        var sender = new LogEmailService(logger, Options.Create(new EmailOptions()));
        Assert.False(sender.IsConfigured);

        await sender.SendEmailAsync(PrivateMessage());

        Assert.NotEmpty(logger.Messages);
        AssertNoPrivateMessage(logger.Messages);
    }

    [Theory]
    [InlineData(false, "smtp.example.test", "user", "password", false)]
    [InlineData(true, "", "user", "password", false)]
    [InlineData(true, "smtp.example.test", "", "password", false)]
    [InlineData(true, "smtp.example.test", "user", "", false)]
    [InlineData(true, "smtp.example.test", "user", "password", true)]
    public void SmtpReadiness_RequiereConfiguracionReal(bool enabled, string host, string username, string password, bool expected)
    {
        var sender = new SmtpEmailService(new CapturedLogger<SmtpEmailService>(), Options.Create(new EmailOptions
        {
            Enabled = enabled, SmtpHost = host, UserName = username, Password = password,
        }));
        Assert.Equal(expected, sender.IsConfigured);
    }

    [Fact]
    public async Task SmtpSinCredenciales_NoRegistraElCuerpoNiDestinatario()
    {
        var logger = new CapturedLogger<SmtpEmailService>();
        var sender = new SmtpEmailService(logger, Options.Create(new EmailOptions()));
        Assert.False(sender.IsConfigured);

        await sender.SendEmailAsync(PrivateMessage());

        Assert.NotEmpty(logger.Messages);
        AssertNoPrivateMessage(logger.Messages);
    }

    [Fact]
    public async Task EmailDispatcher_ExcepcionDelProveedorConPii_NoFiltraSuMensajeAlLog()
    {
        var sender = Substitute.For<IEmailService>();
        sender.IsConfigured.Returns(true);
        sender.SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("private-person@example.test Sofía Vega 4.711,-74.0721")));
        var logger = new CapturedLogger<SosEmailDispatcher>();
        var dedupe = Substitute.For<INotificationDedupeRepository>();
        var alert = new SosAlert
        {
            Id = Guid.NewGuid(),
            Patient = new PatientProfile { FirstName = "Sofía", LastName = "Vega", EmergencyContact = "{\"email\":\"private-person@example.test\"}" },
        };

        var status = await new SosEmailDispatcher(sender, dedupe, logger).DispatchAsync(alert);

        Assert.Equal(SosChannelStatus.Fallido, status);
        Assert.Contains(logger.Messages, message => message.Contains("InvalidOperationException", StringComparison.Ordinal));
        AssertNoPrivateMessage(logger.Messages);
    }

    [Fact]
    public async Task LogSender_CancelacionNoSeConvierteEnEntrega()
    {
        var sender = new LogEmailService(new CapturedLogger<LogEmailService>(), Options.Create(new EmailOptions()));
        await Assert.ThrowsAsync<OperationCanceledException>(() => sender.SendEmailAsync(PrivateMessage(), new CancellationToken(true)));
    }

    private static void AssertNoPrivateMessage(IEnumerable<string> messages)
    {
        foreach (var message in messages)
        {
            Assert.DoesNotContain("private-person", message);
            Assert.DoesNotContain("Sofía", message);
            Assert.DoesNotContain("4.711", message);
            Assert.DoesNotContain("3055550123", message);
            Assert.DoesNotContain("Nombre privado", message);
        }
    }

    private sealed class CapturedLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception) + (exception is null ? "" : " " + exception));
    }
}
