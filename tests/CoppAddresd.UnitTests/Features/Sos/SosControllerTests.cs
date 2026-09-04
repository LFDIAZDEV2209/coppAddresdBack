using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CoppAddresd.Infrastructure.Persistence;
using CoppAddresd.UnitTests.ProgramProgress;
using CoppAddresd.UnitTests.ProgramProgress.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CoppAddresd.UnitTests.Features.Sos;

/// <summary>
/// Tests de integración del endpoint POST /api/v1/sos/alerts:
/// auth JWT, resolución paciente, validaciones y contrato de respuesta.
/// Requieren PostgreSQL real (COP_TEST_DB_CONNECTION); sin la variable se marcan SKIP.
/// </summary>
[Collection(ProgramApiTestCollection.Name)]
public sealed class SosControllerTests(ProgramApiTestDb fixture) : IAsyncDisposable
{
    private readonly Lazy<ProgramApiHost> _host = new(() => ProgramApiHost.Create(fixture));

    static SosControllerTests()
    {
        // Determinismo: canales en modo Log sin importar el appsettings.json local.
        Environment.SetEnvironmentVariable("Sos__SmsProvider", "Log");
        Environment.SetEnvironmentVariable("Sos__VoiceProvider", "Log");
        Environment.SetEnvironmentVariable("Sos__EmailProvider", "Log");
        Environment.SetEnvironmentVariable("Sos__EmergencyNumber", "911");
        Environment.SetEnvironmentVariable("Email__Enabled", "false");
    }

    public async ValueTask DisposeAsync()
    {
        if (_host.IsValueCreated)
        {
            await _host.Value.DisposeAsync();
        }
    }

    [RequiresPostgresFact]
    public async Task Activate_SinToken_Devuelve401()
    {
        var client = _host.Value.CreateClient();

        var response = await client.PostAsync("/api/v1/sos/alerts", ProgramApiSeed.Json(new { language = "es" }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [RequiresPostgresFact]
    public async Task Activate_SinPerfilPaciente_Devuelve404()
    {
        await using var db = fixture.CreateDbContext();
        var userId = await ProgramApiSeed.CreateAuthUserAsync(db);
        var jwt = TestJwt.Mint(userId);
        var client = _host.Value.CreateClient(jwt);

        var response = await client.PostAsync("/api/v1/sos/alerts", ProgramApiSeed.Json(new { language = "es" }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [RequiresPostgresFact]
    public async Task Activate_ConPerfilPaciente_Devuelve200ConContratoYPersiste()
    {
        await using var seedDb = fixture.CreateDbContext();
        var userId = await ProgramApiSeed.CreateAuthUserAsync(seedDb);
        var patientId = await ProgramApiSeed.CreatePatientAsync(seedDb, userId);

        var jwt = TestJwt.Mint(userId, patientId: patientId);
        var client = _host.Value.CreateClient(jwt);

        var response = await client.PostAsync("/api/v1/sos/alerts", ProgramApiSeed.Json(new
        {
            latitude = 25.7617,
            longitude = -80.1918,
            accuracyMeters = 12.5,
            locationLabel = "Test HQ",
            vitals = new { heartRate = 140, spo2 = 94, bloodPressure = "160/110" },
            emergencyContact = new
            {
                name = "Pedro Gonzalez",
                relationship = "Esposo",
                phone = "+1 786 555 0192",
                email = "pedro@test.local"
            },
            language = "es"
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        var id = Guid.Parse(json.GetProperty("id").GetString()!);
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal("Sent", json.GetProperty("status").GetString());
        Assert.Equal("911", json.GetProperty("emergencyNumber").GetString());
        Assert.False(json.GetProperty("triggeredAt").GetString() is null);

        var messageText = json.GetProperty("messageText").GetString() ?? string.Empty;
        Assert.Contains("SOS ALERT - EMERGENCY", messageText);
        Assert.Contains("Ana Prueba", messageText);
        Assert.Contains("Call 911 if needed", messageText);
        Assert.DoesNotContain("maps.google.com", messageText, StringComparison.OrdinalIgnoreCase);

        // Canales: proveedores Log reportan Sent (log + éxito simulado).
        Assert.Equal("Sent", json.GetProperty("sms").GetProperty("status").GetString());
        Assert.Equal("Sent", json.GetProperty("email").GetProperty("status").GetString());
        Assert.Equal("Sent", json.GetProperty("voice").GetProperty("status").GetString());

        // Fila persistida con estado consolidado y resultados por canal.
        await using var verifyDb = fixture.CreateDbContext();
        var row = await verifyDb.SosAlerts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id);
        Assert.NotNull(row);
        Assert.Equal("Sent", row!.Status);
        Assert.Equal(patientId, row.PatientId);
        Assert.Contains("\"sms\"", row.ChannelResults);
    }

    [RequiresPostgresFact]
    public async Task Activate_SinContacto_CanalesSkippedYEstadoDisabled()
    {
        await using var seedDb = fixture.CreateDbContext();
        var userId = await ProgramApiSeed.CreateAuthUserAsync(seedDb);
        var patientId = await ProgramApiSeed.CreatePatientAsync(seedDb, userId);

        var jwt = TestJwt.Mint(userId, patientId: patientId);
        var client = _host.Value.CreateClient(jwt);

        var response = await client.PostAsync("/api/v1/sos/alerts", ProgramApiSeed.Json(new
        {
            latitude = 25.7617,
            longitude = -80.1918,
            language = "es"
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.Equal("Disabled", json.GetProperty("status").GetString());
        Assert.Equal("Skipped", json.GetProperty("sms").GetProperty("status").GetString());
        Assert.Equal("Skipped", json.GetProperty("email").GetProperty("status").GetString());
        Assert.Equal("Skipped", json.GetProperty("voice").GetProperty("status").GetString());
    }

    [RequiresPostgresFact]
    public async Task Activate_LatitudSinLongitud_Devuelve400()
    {
        await using var seedDb = fixture.CreateDbContext();
        var userId = await ProgramApiSeed.CreateAuthUserAsync(seedDb);
        var patientId = await ProgramApiSeed.CreatePatientAsync(seedDb, userId);

        var jwt = TestJwt.Mint(userId, patientId: patientId);
        var client = _host.Value.CreateClient(jwt);

        var response = await client.PostAsync("/api/v1/sos/alerts", ProgramApiSeed.Json(new
        {
            latitude = 25.7617,
            language = "es"
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [RequiresPostgresFact]
    public async Task Activate_IdiomaInvalido_Devuelve400()
    {
        await using var seedDb = fixture.CreateDbContext();
        var userId = await ProgramApiSeed.CreateAuthUserAsync(seedDb);
        var patientId = await ProgramApiSeed.CreatePatientAsync(seedDb, userId);

        var jwt = TestJwt.Mint(userId, patientId: patientId);
        var client = _host.Value.CreateClient(jwt);

        var response = await client.PostAsync("/api/v1/sos/alerts", ProgramApiSeed.Json(new { language = "fr" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
