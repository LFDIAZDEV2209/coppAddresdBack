using System.Text.Json;
using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.Patients;

public class PatientEmergencyContactsTests
{
    private readonly IPatientRepository patients = Substitute.For<IPatientRepository>();
    private readonly ILogger<UpdateMyPatientProfileCommandHandler> logger = Substitute.For<ILogger<UpdateMyPatientProfileCommandHandler>>();
    private readonly Guid user = Guid.NewGuid();
    private UpdateMyPatientProfileCommand Command(SecondaryEmergencyContactDto? second = null) => new(user, null, null, null, "Ana", "Madre", "+573001234567", "ana@example.test", null, null, second);
    private PatientProfile Profile() => new() { Id = Guid.NewGuid(), UserId = user, FirstName = "Paciente", LastName = "Prueba", Status = "Activo" };
    private UpdateMyPatientProfileCommandHandler Handler() => new(patients, logger);

    [Fact]
    public async Task GuardaDosContactos_PrincipalSigueEnRaizParaSos()
    {
        var patient = Profile();
        patients.GetByUserIdAsync(user, Arg.Any<CancellationToken>()).Returns(patient);
        var secondary = new SecondaryEmergencyContactDto("Luis", "Hermano/a", "+12025550123", "backup@example.test");
        var result = await Handler().Handle(Command(secondary), CancellationToken.None);
        Assert.True(result.Success);
        Assert.Equal(secondary, result.Profile!.SecondaryEmergencyContact);
        using var json = JsonDocument.Parse(patient.EmergencyContact!);
        Assert.Equal("+573001234567", json.RootElement.GetProperty("phone").GetString());
        Assert.Equal("+12025550123", json.RootElement.GetProperty("secondary").GetProperty("phone").GetString());
    }

    [Fact]
    public async Task SegundoVacioEsOpcional_YPermiteBorrarloExpresamente()
    {
        var patient = Profile();
        patient.EmergencyContact = EmergencyContactCodec.Serialize(new("Ana", "Madre", "+573001234567", null, new("Luis", null, "+12025550123", null)));
        patients.GetByUserIdAsync(user, Arg.Any<CancellationToken>()).Returns(patient);
        var result = await Handler().Handle(Command(new("", "", "", "")), CancellationToken.None);
        Assert.True(result.Success);
        Assert.Null(result.Profile!.SecondaryEmergencyContact);
        Assert.Equal("Ana", result.Profile.EmergencyName);
    }

    [Fact]
    public async Task EdicionQueOmiteSegundoContacto_LoConserva()
    {
        var patient = Profile();
        var secondary = new SecondaryEmergencyContactDto("Luis", "Hermano/a", "+12025550123", null);
        patient.EmergencyContact = EmergencyContactCodec.Serialize(new("Ana", null, "+573001234567", null, secondary));
        patients.GetByUserIdAsync(user, Arg.Any<CancellationToken>()).Returns(patient);
        var result = await Handler().Handle(Command() with { EmergencyName = "Ana María" }, CancellationToken.None);
        Assert.True(result.Success);
        Assert.Equal(secondary, result.Profile!.SecondaryEmergencyContact);
    }

    [Theory]
    [InlineData("", "+12025550123", "")]
    [InlineData("Luis", "", "")]
    [InlineData("Luis", "+12025550123", "broken")]
    [InlineData("Luis", "invalid phone", "")]
    public async Task RechazaSegundoParcialOMalformado(string name, string phone, string email)
    {
        var result = await Handler().Handle(Command(new(name, "", phone, email)), CancellationToken.None);
        Assert.False(result.Success);
        await patients.DidNotReceive().UpdateAsync(Arg.Any<PatientProfile>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("", "+573001234567")]
    [InlineData("Ana", "")]
    public async Task NoPermiteEliminarPrincipal(string name, string phone)
    {
        var result = await Handler().Handle(Command() with { EmergencyName = name, EmergencyPhone = phone }, CancellationToken.None);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task SinSeguro_EliminaAseguradoraAnterior_SinConfundirNullLegacy()
    {
        var patient = Profile();
        var insurer = Guid.NewGuid();
        patient.InsurerId = insurer;
        patients.GetByUserIdAsync(user, Arg.Any<CancellationToken>()).Returns(patient);
        Assert.True((await Handler().Handle(Command(), CancellationToken.None)).Success);
        Assert.Equal(insurer, patient.InsurerId);
        Assert.True((await Handler().Handle(Command() with { ClearInsurer = true }, CancellationToken.None)).Success);
        Assert.Null(patient.InsurerId);
    }

    [Fact]
    public async Task OnboardingNoPromuevePacienteSinContactoPrincipal()
    {
        var patient = Profile();
        patient.Status = "Pendiente";
        patients.GetByUserIdAsync(user, Arg.Any<CancellationToken>()).Returns(patient);
        var command = Command() with { EmergencyName = null, EmergencyRelationship = null, EmergencyPhone = null, EmergencyEmail = null };
        var result = await Handler().Handle(command, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal("Pendiente", patient.Status);
        await patients.DidNotReceive().UpdateAsync(Arg.Any<PatientProfile>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Codec_RoundtripSegundoContacto_SinCambiarContratoPrincipal()
    {
        var contact = new EmergencyContactDto("Ana", "Madre", "+573001234567", null, new("Luis", null, "+12025550123", null));
        Assert.Equal(contact, EmergencyContactCodec.Deserialize(EmergencyContactCodec.Serialize(contact)));
    }
}
