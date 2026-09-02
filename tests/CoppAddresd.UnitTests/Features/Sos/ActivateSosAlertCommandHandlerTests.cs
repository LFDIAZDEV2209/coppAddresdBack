using CoppAddresd.Application.DTOs.Email;
using CoppAddresd.Application.Features.Sos;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Services.Sos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CoppAddresd.UnitTests.Features.Sos;

/// <summary>
/// Pruebas unitarias del handler de activación SOS. Usa fakes (NSubstitute)
/// para repositorios y servicios de envío — sin base de datos.
/// </summary>
public class ActivateSosAlertCommandHandlerTests
{
    private readonly IPatientRepository _patientRepository = Substitute.For<IPatientRepository>();
    private readonly ISosAlertRepository _sosAlertRepository = Substitute.For<ISosAlertRepository>();
    private readonly ISmsSender _smsSender = Substitute.For<ISmsSender>();
    private readonly IVoiceCaller _voiceCaller = Substitute.For<IVoiceCaller>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly ILogger<ActivateSosAlertCommandHandler> _logger =
        Substitute.For<ILogger<ActivateSosAlertCommandHandler>>();

    private readonly ActivateSosAlertCommandHandler _handler;

    public ActivateSosAlertCommandHandlerTests()
    {
        var sosOptions = Options.Create(new SosOptions
        {
            EmergencyNumber = "911",
            SmsProvider = "Log",
            EmailProvider = "Log",
            VoiceProvider = "Log"
        });

        _handler = new ActivateSosAlertCommandHandler(
            _patientRepository,
            _sosAlertRepository,
            _smsSender,
            _voiceCaller,
            _emailService,
            sosOptions,
            _logger);
    }

    // ========================================================================
    // (a) Mensaje contiene nombre + mapa y nunca "null"
    // ========================================================================

    [Fact]
    public async Task Handle_ConCoordenadas_MensajeContieneNombreYMapaLink()
    {
        // Arrange
        var patientId = Guid.NewGuid();
        var patient = CreatePatient(patientId, "María", "Elena", "González");
        _patientRepository.GetByIdAsync(patientId, Arg.Any<CancellationToken>()).Returns(patient);

        var alertId = Guid.NewGuid();
        _sosAlertRepository
            .AddAsync(Arg.Any<SosAlert>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<SosAlert>());

        var request = new ActivateSosAlertRequest(
            Latitude: 4.7110,
            Longitude: -74.0721,
            AccuracyMeters: 15,
            LocationLabel: "Calle 100, Bogotá",
            Vitals: new SosVitals(HeartRate: 85, Spo2: 97, BloodPressure: "120/80"),
            EmergencyContact: new SosEmergencyContact("Juan Pérez", "Esposo", "+573001234567", "juan@test.com"),
            Language: "es");

        // Act
        var result = await _handler.Handle(
            new ActivateSosAlertCommand(patientId, request),
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("María", result.MessageText);
        Assert.Contains("Elena", result.MessageText);
        Assert.Contains("González", result.MessageText);
        Assert.Contains("https://maps.google.com/?q=4.711,-74.0721", result.MessageText);
        Assert.Contains("15 metros", result.MessageText);
        Assert.DoesNotContain("null", result.MessageText);
        Assert.Equal("911", result.EmergencyNumber);
    }

    [Fact]
    public async Task Handle_SinCoordenadas_MensajeContieneUbicacionNoDisponible()
    {
        // Arrange
        var patientId = Guid.NewGuid();
        var patient = CreatePatient(patientId, "Carlos", null, "Ruiz");
        _patientRepository.GetByIdAsync(patientId, Arg.Any<CancellationToken>()).Returns(patient);

        _sosAlertRepository
            .AddAsync(Arg.Any<SosAlert>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<SosAlert>());

        var request = new ActivateSosAlertRequest(
            Latitude: null,
            Longitude: null,
            AccuracyMeters: null,
            LocationLabel: null,
            Vitals: null,
            EmergencyContact: new SosEmergencyContact("Ana", "Madre", "+573009876543", "ana@test.com"),
            Language: "es");

        // Act
        var result = await _handler.Handle(
            new ActivateSosAlertCommand(patientId, request),
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("ubicacion no disponible", result.MessageText);
        Assert.DoesNotContain("null", result.MessageText);
    }

    [Fact]
    public async Task Handle_PacienteSinOpcionales_MensajeSinCamposNulos()
    {
        // Arrange
        var patientId = Guid.NewGuid();
        var patient = CreatePatient(patientId, "Test", null, "User");
        patient.DocumentNumber = null;
        patient.BloodType = null;
        patient.Insurer = null;
        patient.MemberId = null;
        _patientRepository.GetByIdAsync(patientId, Arg.Any<CancellationToken>()).Returns(patient);

        _sosAlertRepository
            .AddAsync(Arg.Any<SosAlert>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<SosAlert>());

        var request = new ActivateSosAlertRequest(
            Latitude: 1.0, Longitude: 2.0, AccuracyMeters: null,
            LocationLabel: null, Vitals: null, EmergencyContact: null,
            Language: "en");

        // Act
        var result = await _handler.Handle(
            new ActivateSosAlertCommand(patientId, request),
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.DoesNotContain("null", result.MessageText);
        Assert.DoesNotContain("Tipo sangre", result.MessageText);
        Assert.DoesNotContain("Aseguradora", result.MessageText);
    }

    // ========================================================================
    // (b) Todos los canales Sent → status "Sent"
    // ========================================================================

    [Fact]
    public async Task Handle_TodosCanalesSent_EstadoSent()
    {
        // Arrange
        var patientId = Guid.NewGuid();
        var patient = CreatePatient(patientId, "Ana", "María", "López");
        _patientRepository.GetByIdAsync(patientId, Arg.Any<CancellationToken>()).Returns(patient);

        _sosAlertRepository
            .AddAsync(Arg.Any<SosAlert>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<SosAlert>());

        var request = new ActivateSosAlertRequest(
            Latitude: 4.0, Longitude: -74.0, AccuracyMeters: 10,
            LocationLabel: null, Vitals: null,
            EmergencyContact: new SosEmergencyContact("Pedro", "Padre", "+573001112233", "pedro@test.com"),
            Language: "es");

        // Act
        var result = await _handler.Handle(
            new ActivateSosAlertCommand(patientId, request),
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Sent", result.Status);
        Assert.Equal("Sent", result.Sms.Status);
        Assert.Equal("Sent", result.Email.Status);
        Assert.Equal("Sent", result.Voice.Status);
    }

    // ========================================================================
    // (c) Un canal Failed → status "Partial"
    // ========================================================================

    [Fact]
    public async Task Handle_SmsFalla_EstadoPartial()
    {
        // Arrange
        var patientId = Guid.NewGuid();
        var patient = CreatePatient(patientId, "Luis", null, "Martínez");
        _patientRepository.GetByIdAsync(patientId, Arg.Any<CancellationToken>()).Returns(patient);

        _sosAlertRepository
            .AddAsync(Arg.Any<SosAlert>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<SosAlert>());

        // SMS falla con excepción genérica (no InvalidOperationException, que se interpreta como Disabled)
        _smsSender
            .SendAsync(Arg.Any<SmsMessage>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Error de red Twilio"));

        var request = new ActivateSosAlertRequest(
            Latitude: null, Longitude: null, AccuracyMeters: null,
            LocationLabel: null, Vitals: null,
            EmergencyContact: new SosEmergencyContact("Rosa", "Hermana", "+573004445566", "rosa@test.com"),
            Language: "es");

        // Act
        var result = await _handler.Handle(
            new ActivateSosAlertCommand(patientId, request),
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Partial", result.Status);
        Assert.Equal("Failed", result.Sms.Status);
        Assert.Equal("Sent", result.Email.Status);
        Assert.Equal("Sent", result.Voice.Status);
    }

    // ========================================================================
    // (d) Sin teléfono ni email → Skipped → "Disabled"
    // ========================================================================

    [Fact]
    public async Task Handle_SinContactoTodosSkipped_EstadoDisabled()
    {
        // Arrange
        var patientId = Guid.NewGuid();
        var patient = CreatePatient(patientId, "Laura", "Isabel", "Sánchez");
        _patientRepository.GetByIdAsync(patientId, Arg.Any<CancellationToken>()).Returns(patient);

        _sosAlertRepository
            .AddAsync(Arg.Any<SosAlert>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<SosAlert>());

        // Contacto con nombre pero sin phone ni email
        var request = new ActivateSosAlertRequest(
            Latitude: null, Longitude: null, AccuracyMeters: null,
            LocationLabel: null, Vitals: null,
            EmergencyContact: new SosEmergencyContact("Contacto", "Amigo", "", ""),
            Language: "es");

        // Act
        var result = await _handler.Handle(
            new ActivateSosAlertCommand(patientId, request),
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Disabled", result.Status);
        Assert.Equal("Skipped", result.Sms.Status);
        Assert.Equal("Skipped", result.Email.Status);
        Assert.Equal("Skipped", result.Voice.Status);
    }

    [Fact]
    public async Task Handle_SinContactoAlguno_CanalSkippedYLlamadoExitoso()
    {
        // Arrange
        var patientId = Guid.NewGuid();
        var patient = CreatePatient(patientId, "Roberto", null, "Díaz");
        _patientRepository.GetByIdAsync(patientId, Arg.Any<CancellationToken>()).Returns(patient);

        _sosAlertRepository
            .AddAsync(Arg.Any<SosAlert>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<SosAlert>());

        // Solo email, sin phone (phone es whitespace → Skipped)
        var request = new ActivateSosAlertRequest(
            Latitude: null, Longitude: null, AccuracyMeters: null,
            LocationLabel: null, Vitals: null,
            EmergencyContact: new SosEmergencyContact("SoloEmail", "Padre", "  ", "solo@email.com"),
            Language: "es");

        // Act
        var result = await _handler.Handle(
            new ActivateSosAlertCommand(patientId, request),
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Skipped", result.Sms.Status);
        Assert.Equal("Sent", result.Email.Status);
        Assert.Equal("Skipped", result.Voice.Status);
        // Email Sent + no Failed → "Sent"
        Assert.Equal("Sent", result.Status);
    }

    // ========================================================================
    // (e) Proveedor Log → "Disabled"
    // ========================================================================

    [Fact]
    public async Task Handle_ProveedorLog_TodosSent()
    {
        // Arrange
        var patientId = Guid.NewGuid();
        var patient = CreatePatient(patientId, "Pedro", "Antonio", "Ramírez");
        _patientRepository.GetByIdAsync(patientId, Arg.Any<CancellationToken>()).Returns(patient);

        _sosAlertRepository
            .AddAsync(Arg.Any<SosAlert>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<SosAlert>());

        var request = new ActivateSosAlertRequest(
            Latitude: null, Longitude: null, AccuracyMeters: null,
            LocationLabel: null, Vitals: null,
            EmergencyContact: new SosEmergencyContact("Contacto", "Familiar", "+573001231234", "test@test.com"),
            Language: "es");

        // Act
        var result = await _handler.Handle(
            new ActivateSosAlertCommand(patientId, request),
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        // Con providers "Log" (fakes de NSubstitute no lanzan excepción)
        // todos los canales exitosos → status "Sent"
        Assert.Equal("Sent", result.Status);
        Assert.Equal("Sent", result.Sms.Status);
        Assert.Equal("Sent", result.Email.Status);
        Assert.Equal("Sent", result.Voice.Status);
    }

    // ========================================================================
    // (f) Paciente no encontrado → null
    // ========================================================================

    [Fact]
    public async Task Handle_PacienteNoExiste_RetornaNull()
    {
        // Arrange
        var patientId = Guid.NewGuid();
        _patientRepository.GetByIdAsync(patientId, Arg.Any<CancellationToken>()).Returns((PatientProfile?)null);

        var request = new ActivateSosAlertRequest(
            Latitude: null, Longitude: null, AccuracyMeters: null,
            LocationLabel: null, Vitals: null, EmergencyContact: null,
            Language: "es");

        // Act
        var result = await _handler.Handle(
            new ActivateSosAlertCommand(patientId, request),
            CancellationToken.None);

        // Assert
        Assert.Null(result);
        await _sosAlertRepository.DidNotReceive().AddAsync(Arg.Any<SosAlert>(), Arg.Any<CancellationToken>());
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    private static PatientProfile CreatePatient(
        Guid id, string firstName, string? middleName, string lastName)
    {
        return new PatientProfile
        {
            Id = id,
            UserId = Guid.NewGuid(),
            FirstName = firstName,
            MiddleName = middleName,
            LastName = lastName,
            DateOfBirth = new DateTime(1990, 5, 15, 0, 0, 0, DateTimeKind.Utc),
            DocumentNumber = "1234567890",
            BloodType = new BloodType { Code = "O+" },
            Insurer = new Insurer { Name = "_eps_test" },
            MemberId = "MEM-001",
            Status = "Activo"
        };
    }
}
