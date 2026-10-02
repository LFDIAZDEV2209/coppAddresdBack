using System.Text.Json;
using CoppAddresd.Application.Features.Patients;

namespace CoppAddresd.UnitTests.Features.Patients;

/// <summary>
/// Codec del contacto de emergencia hacia la columna <c>emergency_contact</c>:
/// JSON en minúsculas compatible con la app móvil, null cuando no hay nombre
/// ni teléfono y lectura tolerante de datos legacy en texto plano.
/// </summary>
public class EmergencyContactCodecTests
{
    [Fact]
    public void Serialize_SinNombreNiTelefono_DevuelveNull()
    {
        Assert.Null(EmergencyContactCodec.Serialize(null));
        Assert.Null(EmergencyContactCodec.Serialize(new EmergencyContactDto(" ", "Madre", " ", "a@b.co")));
    }

    [Fact]
    public void Serialize_ConTelefonoSinNombre_ConservaTelefono()
    {
        var json = EmergencyContactCodec.Serialize(
            new EmergencyContactDto(null, null, "5551234567", null)
        );

        Assert.NotNull(json);
        using var document = JsonDocument.Parse(json!);
        Assert.Equal("5551234567", document.RootElement.GetProperty("phone").GetString());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("name").ValueKind);
    }

    [Fact]
    public void Serialize_UsaClavesEnMinusculas()
    {
        var json = EmergencyContactCodec.Serialize(
            new EmergencyContactDto("Ana", "Madre", "5551234567", "ana@example.com")
        );

        Assert.NotNull(json);
        using var document = JsonDocument.Parse(json!);
        var root = document.RootElement;
        Assert.Equal("Ana", root.GetProperty("name").GetString());
        Assert.Equal("Madre", root.GetProperty("relationship").GetString());
        Assert.Equal("5551234567", root.GetProperty("phone").GetString());
        Assert.Equal("ana@example.com", root.GetProperty("email").GetString());
    }

    [Fact]
    public void RoundTrip_ConservaTodosLosCampos()
    {
        var original = new EmergencyContactDto("Ana", "Madre", "5551234567", "ana@example.com");

        var result = EmergencyContactCodec.Deserialize(EmergencyContactCodec.Serialize(original));

        Assert.Equal(original, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Deserialize_Vacio_DevuelveNull(string? value)
    {
        Assert.Null(EmergencyContactCodec.Deserialize(value));
    }

    [Fact]
    public void Deserialize_TextoLegacy_SeTrataComoNombre()
    {
        var result = EmergencyContactCodec.Deserialize("Juan Pérez 555-1234");

        Assert.NotNull(result);
        Assert.Equal("Juan Pérez 555-1234", result!.Name);
        Assert.Null(result.Relationship);
        Assert.Null(result.Phone);
        Assert.Null(result.Email);
    }

    [Fact]
    public void Deserialize_JsonInvalido_SeTrataComoNombre()
    {
        var result = EmergencyContactCodec.Deserialize("{no-es-json");

        Assert.NotNull(result);
        Assert.Equal("{no-es-json", result!.Name);
    }
}
