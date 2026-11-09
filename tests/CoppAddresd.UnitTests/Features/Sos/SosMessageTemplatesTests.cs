using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Services;

namespace CoppAddresd.UnitTests.Features.Sos;

public sealed class SosMessageTemplatesTests
{
    private static SosAlert Alert() => new()
    {
        Id = Guid.Parse("5c396a6e-b0dd-4a01-b239-e16cec40f7ed"),
        CreatedAt = new DateTime(2026, 11, 3, 18, 25, 0, DateTimeKind.Utc),
        Latitude = 4.711,
        Longitude = -74.0721,
        AccuracyMeters = 27.7,
        Patient = new PatientProfile
        {
            FirstName = "Sofía",
            LastName = "Vega",
            DocumentNumber = "private-document-12345",
            DateOfBirth = new DateTime(1981, 4, 3),
            PhoneCountryCode = "57",
            PhoneNumber = "305 392 4819",
            Address = "Dirección registrada que no es necesariamente la ubicación actual",
        },
    };

    [Fact]
    public void Sms_AbreConLaPersonaYLaAccion_SinDocumentoNiReferenciaInterna()
    {
        var alert = Alert();
        var sms = SosSmsTemplate.Build(alert);

        Assert.StartsWith("SOS Copp Adresd: Sofia Vega\n", sms);
        Assert.Contains("Llamar: +573053924819", sms);
        Assert.DoesNotContain(alert.Patient!.DocumentNumber!, sms);
        Assert.DoesNotContain(alert.Id.ToString()[..8], sms);
        Assert.DoesNotContain("años", sms);
        Assert.DoesNotContain("±", sms);
        Assert.DoesNotContain(alert.Patient.Address!, sms);
    }

    [Fact]
    public void Sms_EnlacePropio_NoExponeCoordenadas()
    {
        const string sharedLink = "https://erp.example.test/api/v1/sos/maps/opaque-token";
        var sms = SosSmsTemplate.Build(Alert(), sharedLink);

        Assert.Contains(sharedLink, sms);
        Assert.DoesNotContain("4.711", sms);
        Assert.DoesNotContain("-74.0721", sms);
        Assert.DoesNotContain("maps.google.com", sms);
    }

    [Fact]
    public void MapsLink_UsaUnaUrlValida_SinSufijoDePrecisionNiFormatoRegional()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-CO");
            var link = SosMessageFormatting.MapsLink(Alert());
            Assert.True(Uri.TryCreate(link, UriKind.Absolute, out var uri));
            Assert.Equal("https", uri.Scheme);
            Assert.Equal("?q=4.711,-74.0721", uri.Query);
            Assert.DoesNotContain("27.7", link);
            Assert.DoesNotContain(" ", link);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData(double.NaN, -74)]
    [InlineData(4, double.PositiveInfinity)]
    [InlineData(91, -74)]
    [InlineData(4, -181)]
    public void MapsLink_CoordenadasInvalidas_NoPublicaUnEnlace(double latitude, double longitude)
    {
        var alert = Alert();
        alert.Latitude = latitude;
        alert.Longitude = longitude;
        Assert.Null(SosMessageFormatting.MapsLink(alert));
    }

    [Fact]
    public void Email_OfreceLlamadaYMapa_SinCoordenadasVisiblesNiDatosAdministrativos()
    {
        var alert = Alert();
        var html = SosEmailTemplate.BuildHtml(alert);
        var visible = WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]*>", " "));

        Assert.Contains("Sofía Vega necesita tu ayuda", visible);
        Assert.Contains("Llamar a Sofía", visible);
        Assert.Contains("Ver ubicación en el mapa", visible);
        Assert.Contains("tel:+573053924819", html);
        Assert.Contains("18:25 UTC", visible);
        Assert.DoesNotContain("4.711", visible);
        Assert.DoesNotContain("-74.0721", visible);
        Assert.DoesNotContain(alert.Patient!.DocumentNumber!, html);
        Assert.DoesNotContain(alert.Id.ToString()[..8], html);
        Assert.DoesNotContain("<img", html);
    }

    [Fact]
    public void Email_EscapaDatosDinamicos_NoCreaHtmlEjecutable()
    {
        var alert = Alert();
        alert.Patient!.FirstName = "<img src=x onerror=alert(1)>";
        alert.Patient.LastName = "Vega & familia";
        alert.BloodPressure = "<script>alert(1)</script>";
        var html = SosEmailTemplate.BuildHtml(alert);

        Assert.Contains("&lt;img", html);
        Assert.Contains("&amp; familia", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<img", html);
        Assert.DoesNotContain("<script", html);
    }

    [Fact]
    public void Email_SinGps_NoUsaLaDireccionDelPerfilComoUbicacionActual()
    {
        var alert = Alert();
        alert.Latitude = null;
        alert.Longitude = null;
        var html = SosEmailTemplate.BuildHtml(alert);

        Assert.Contains("Ubicación no compartida", html);
        Assert.DoesNotContain("maps.google.com", html);
        Assert.DoesNotContain("Ver ubicación en el mapa", html);
        Assert.DoesNotContain(alert.Patient!.Address!, html);
        Assert.Contains("Ubicacion no compartida.", SosSmsTemplate.Build(alert));
    }

    [Fact]
    public void Templates_SinLecturas_NoInventanSignosVitales()
    {
        var alert = Alert();
        Assert.DoesNotContain("Lecturas compartidas", SosSmsTemplate.Build(alert));
        Assert.DoesNotContain("Lecturas compartidas", SosEmailTemplate.BuildHtml(alert));
        Assert.DoesNotContain("140", SosEmailTemplate.BuildText(alert));
    }

    [Fact]
    public void Templates_ConLecturas_MuestranSoloLosValoresReportados()
    {
        var alert = Alert();
        alert.HeartRate = 72;
        alert.Spo2 = 98;
        alert.BloodPressure = "118/76";
        var sms = SosSmsTemplate.Build(alert);
        var html = SosEmailTemplate.BuildHtml(alert);

        foreach (var value in new[] { "72 lpm", "98%", "118/76 mmHg" })
        {
            Assert.Contains(value, html);
        }
        Assert.Contains("Lecturas compartidas", html);
        Assert.DoesNotContain("Lecturas compartidas", sms);
    }

    [Theory]
    [InlineData("57", "305 392 4819", "+573053924819")]
    [InlineData("1", "305-555-0123", "+13055550123")]
    [InlineData(null, "+1 (305) 555-0123", "+13055550123")]
    [InlineData(null, "3055550123", null)]
    [InlineData("1", "javascript:alert(1)", null)]
    [InlineData("1", "555", null)]
    [InlineData("001", "3055550123", null)]
    public void PatientPhone_RespetaElCodigoDelPacienteYSoloAceptaTelefonos(string? country, string number, string? expected)
    {
        var patient = new PatientProfile { PhoneCountryCode = country, PhoneNumber = number };
        Assert.Equal(expected, SosMessageFormatting.PatientPhone(patient));
    }

    [Fact]
    public void Email_SinTelefonoValido_NoOfreceUnaLlamadaIncorrecta()
    {
        var alert = Alert();
        alert.Patient!.PhoneNumber = null;
        var html = SosEmailTemplate.BuildHtml(alert);
        Assert.DoesNotContain("tel:", html);
        Assert.Contains("Usa el número de contacto", html);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("https://user:password@example.test/path")]
    public void LocationLink_EsquemaNoSeguroOConCredenciales_UsaMapaSeguro(string invalid)
    {
        var html = SosEmailTemplate.BuildHtml(Alert(), invalid);
        Assert.Contains("https://maps.google.com/", html);
        Assert.DoesNotContain(invalid, html);
    }

    [Fact]
    public void Sms_NombreConSaltosDeLinea_NoPuedeInyectarSecciones()
    {
        var alert = Alert();
        alert.Patient!.FirstName = "Sofía\r\nVega";
        alert.Patient.LastName = "López";
        var sms = SosSmsTemplate.Build(alert);
        Assert.StartsWith("SOS Copp Adresd: Sofia Vega Lopez", sms);
        Assert.DoesNotContain("\r", sms);
    }

    [Fact]
    public void FormatMessageTime_FechaUtcSinKind_NoDependeDeLaZonaDelServidor()
    {
        var unspecified = new DateTime(2026, 11, 3, 18, 25, 0, DateTimeKind.Unspecified);
        Assert.Contains("18:25 UTC", SosMessageFormatting.FormatMessageTime(unspecified));
    }
}
