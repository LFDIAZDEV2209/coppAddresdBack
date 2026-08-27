using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace CoppAddresd.UnitTests.ProgramProgress.Api;

/// <summary>
/// Tests de contrato del motor de puntajes (T-39, SPEC §13.7): la FORMA del
/// JSON de <c>GET /program/scores</c> (§13.7.1, vía <c>JsonElement</c>, sin
/// igualdad profunda frágil), el header <c>X-Score-Recalculated: true</c> del
/// recálculo manual (§13.7.2), la autorización (403 paciente / 200 clínico en
/// <c>POST /calculate</c>), el 404 sin inscripción activa y el 422 de período
/// futuro.
/// </summary>
[Collection(ProgramApiTestCollection.Name)]
public sealed class ScoresContractTests(ProgramApiTestDb fixture)
{
    private readonly Lazy<ProgramApiHost> _host =
        new(() => ProgramApiHost.Create(fixture));

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_host.IsValueCreated)
        {
            await _host.Value.DisposeAsync();
        }
    }

    /// <summary>
    /// GET /program/scores con un JWT de paciente (con <c>Program.View</c> y
    /// <c>patient_id</c>) → 200 con la forma SPEC §13.7.1: health_score
    /// (current/previous/trend/dimensions) y transformation_score
    /// (current/previous/trend/week/detail).
    /// </summary>
    [RequiresPostgresFact]
    public async Task GetScores_PacienteActivo_DevuelveFormaSpec1371()
    {
        var (client, patientId) = await CreateEnrolledPatientClientAsync();

        var response = await client.GetAsync("/api/v1/program/scores");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("health_score", out var health));
        Assert.Equal(JsonValueKind.Number, health.GetProperty("current").ValueKind);
        Assert.True(health.TryGetProperty("previous", out _)); // puede ser null
        Assert.Equal(JsonValueKind.String, health.GetProperty("trend").ValueKind);
        Assert.True(health.TryGetProperty("dimensions", out var dimensions));
        Assert.Equal(JsonValueKind.Number, dimensions.GetProperty("adherence").ValueKind);
        Assert.Equal(JsonValueKind.Number, dimensions.GetProperty("clinical").ValueKind);
        Assert.Equal(JsonValueKind.Number, dimensions.GetProperty("nutrition").ValueKind);
        Assert.Equal(JsonValueKind.Number, dimensions.GetProperty("psychology").ValueKind);
        Assert.Equal(JsonValueKind.Number, dimensions.GetProperty("exercise").ValueKind);

        Assert.True(root.TryGetProperty("transformation_score", out var transformation));
        Assert.Equal(JsonValueKind.Number, transformation.GetProperty("current").ValueKind);
        Assert.True(transformation.TryGetProperty("previous", out _));
        Assert.Equal(JsonValueKind.String, transformation.GetProperty("trend").ValueKind);
        Assert.Equal(JsonValueKind.Number, transformation.GetProperty("week").ValueKind);
        Assert.True(transformation.TryGetProperty("detail", out var detail));
        Assert.Equal(JsonValueKind.Object, detail.ValueKind);
    }

    /// <summary>
    /// POST /program/scores/calculate con JWT de clínico (Program.Edit) →
    /// 200 con el mismo shape y el header <c>X-Score-Recalculated: true</c>
    /// (SPEC §13.7.2).
    /// </summary>
    [RequiresPostgresFact]
    public async Task CalculateScores_Clinico_Devuelve200ConHeader()
    {
        var (patientId, _) = await CreateEnrolledPatientAsync();
        var clinician = Guid.NewGuid();
        var client = _host.Value.CreateClient(
            TestJwt.Mint(clinician, permissions: ["Program.Edit"]));

        var response = await client.PostAsync("/api/v1/program/scores/calculate",
            ProgramApiSeed.Json(new { patientId }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Score-Recalculated", out var values));
        Assert.Contains("true", values);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("health_score", out _));
        Assert.True(doc.RootElement.TryGetProperty("transformation_score", out _));
    }

    /// <summary>
    /// POST /program/scores/calculate con JWT de paciente (sin Program.Edit)
    /// → 403 FORBIDDEN (SPEC §13.6: el recálculo es exclusivo de clínico).
    /// </summary>
    [RequiresPostgresFact]
    public async Task CalculateScores_PacienteSinPermiso_Devuelve403()
    {
        var (patientId, _) = await CreateEnrolledPatientAsync();
        var patientUser = Guid.NewGuid();
        var client = _host.Value.CreateClient(
            TestJwt.Mint(patientUser, patientId: patientId));

        var response = await client.PostAsync("/api/v1/program/scores/calculate",
            ProgramApiSeed.Json(new { patientId }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>GET /scores sin inscripción activa → 404 NO_ACTIVE_ENROLLMENT.</summary>
    [RequiresPostgresFact]
    public async Task GetScores_SinInscripcionActiva_Devuelve404()
    {
        await using var db = fixture.CreateDbContext();
        var user = await ProgramApiSeed.CreateAuthUserAsync(db);
        var patientId = await ProgramApiSeed.CreatePatientAsync(db, user, "SinScores");

        var client = _host.Value.CreateClient(
            TestJwt.Mint(user, permissions: ["Program.View"], patientId: patientId));

        var response = await client.GetAsync("/api/v1/program/scores");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Contains("NO_ACTIVE_ENROLLMENT", doc.RootElement.GetProperty("detail").GetString());
    }

    /// <summary>
    /// POST /calculate con <c>periodEndLocalDate</c> futura → 422
    /// <c>INVALID_PERIOD</c> (SPEC §13.7.2; la autoridad del tiempo local es
    /// el repositorio, nunca DateTime.UtcNow del handler).
    /// </summary>
    [RequiresPostgresFact]
    public async Task CalculateScores_PeriodoFuturo_Devuelve422InvalidPeriod()
    {
        var (patientId, _) = await CreateEnrolledPatientAsync();
        var clinician = Guid.NewGuid();
        var client = _host.Value.CreateClient(
            TestJwt.Mint(clinician, permissions: ["Program.Edit"]));

        var future = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);
        var response = await client.PostAsync("/api/v1/program/scores/calculate",
            ProgramApiSeed.Json(new { patientId, periodEndLocalDate = future.ToString("yyyy-MM-dd") }));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Contains("INVALID_PERIOD", doc.RootElement.GetProperty("detail").GetString());
    }

    // ------------------------------------------------------------------ setup

    private async Task<(HttpClient Client, Guid PatientId)> CreateEnrolledPatientClientAsync()
    {
        var (patientId, _) = await CreateEnrolledPatientAsync();
        var patientUser = Guid.NewGuid();
        var client = _host.Value.CreateClient(
            TestJwt.Mint(patientUser, permissions: ["Program.View"], patientId: patientId));
        return (client, patientId);
    }

    /// <summary>
    /// Paciente con usuario auth + perfil + plantilla + inscripción activa que
    /// arranca el lunes de la semana actual (el período de 7 días del Índice
    /// de Salud es determinista).
    /// </summary>
    private async Task<(Guid PatientId, Guid EnrollmentId)> CreateEnrolledPatientAsync()
    {
        await using var db = fixture.CreateDbContext();
        var user = await ProgramApiSeed.CreateAuthUserAsync(db);
        var patientId = await ProgramApiSeed.CreatePatientAsync(db, user, "Scores");
        var templateId = await ProgramApiSeed.SeedTemplateAsync(db);
        var enrollmentId = await ProgramApiSeed.EnrollAsync(db, patientId, templateId, ProgramApiSeed.ThisMonday());
        return (patientId, enrollmentId);
    }
}