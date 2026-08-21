using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Reglas del encuentro clínico (EncounterSupport): serialización jsonb,
/// máquina de estados (Draft → Completed) y contenido mínimo para completar.
/// </summary>
public class EncounterSupportTests
{
    [Fact]
    public void Serialize_DatosCompletos_CamelCaseYMinimal()
    {
        var data = new ClinicalDataDto("Dolor abdominal", null, "Gastritis", null, null, null, null);

        var json = EncounterSupport.Serialize(data);

        Assert.NotNull(json);
        Assert.Contains("\"motivoConsulta\"", json);
        Assert.Contains("\"diagnostico\"", json);
        // Ignora nulos: evaluacion/plan/etc. no deben aparecer.
        Assert.DoesNotContain("evaluacion", json);
        Assert.DoesNotContain("plan", json);
    }

    [Fact]
    public void Serialize_Null_DevuelveNull()
        => Assert.Null(EncounterSupport.Serialize(null));

    [Fact]
    public void Deserialize_JsonValido_Reconstruye()
    {
        const string json = """{"motivoConsulta":"Tos","diagnostico":"Bronquitis"}""";

        var data = EncounterSupport.Deserialize(json);

        Assert.NotNull(data);
        Assert.Equal("Tos", data!.MotivoConsulta);
        Assert.Equal("Bronquitis", data.Diagnostico);
    }

    [Fact]
    public void Deserialize_CamposDesconocidos_Tolerante()
    {
        // El ERP puede ampliar el esquema sin romper (jsonb tolerante).
        const string json = """{"motivoConsulta":"Tos","campoFuturo":{"x":1}}""";

        var data = EncounterSupport.Deserialize(json);

        Assert.NotNull(data);
        Assert.Equal("Tos", data!.MotivoConsulta);
    }

    [Theory]
    [InlineData(AppointmentStatus.InProgress)]
    [InlineData(AppointmentStatus.Completed)]
    public void EnsureCanDocument_EstadosValidos_NoLanza(AppointmentStatus status)
        => EncounterSupport.EnsureCanDocument(status);

    [Theory]
    [InlineData(AppointmentStatus.Requested)]
    [InlineData(AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public void EnsureCanDocument_EstadosInvalidos_Lanza(AppointmentStatus status)
    {
        var ex = Assert.Throws<BusinessRuleViolationException>(() =>
            EncounterSupport.EnsureCanDocument(status));
        Assert.Contains(status.ToString(), ex.Message);
    }

    [Fact]
    public void EnsureEditable_Completed_Lanza()
        => Assert.Throws<BusinessRuleViolationException>(() =>
            EncounterSupport.EnsureEditable(EncounterStatus.Completed));

    [Theory]
    [InlineData(EncounterStatus.Draft)]
    [InlineData(EncounterStatus.Cancelled)]
    public void EnsureEditable_NoCompleted_NoLanza(EncounterStatus status)
        => EncounterSupport.EnsureEditable(status);

    [Fact]
    public void HasContent_ConNota_EsVerdadero()
        => Assert.True(EncounterSupport.HasContent(null, "Nota clínica"));

    [Fact]
    public void HasContent_ConCampoClinico_EsVerdadero()
        => Assert.True(EncounterSupport.HasContent(
            new ClinicalDataDto(null, null, null, "Plan: reposo", null, null, null), null));

    [Fact]
    public void HasContent_Vacio_EsFalso()
        => Assert.False(EncounterSupport.HasContent(
            new ClinicalDataDto(null, null, null, null, null, null, null), null));

    [Fact]
    public void HasContent_NotaEnBlanco_YDatosVacios_EsFalso()
        => Assert.False(EncounterSupport.HasContent(null, "   "));
}
