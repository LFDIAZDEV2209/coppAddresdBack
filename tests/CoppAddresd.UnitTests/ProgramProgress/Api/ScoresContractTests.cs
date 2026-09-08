using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Infrastructure.Persistence;

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
        // Aditivo (SPEC §13.7.1): puede ser null (primer cómputo) pero el campo
        // SIEMPRE está presente en el contrato.
        Assert.True(health.TryGetProperty("dimensions_previous", out _));

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

    // ------------------------------------------------------------- scores-history

    /// <summary>
    /// GET /program/me/scores-history con 3 semanas sembradas (1: salud 21 +
    /// transformación 0; 2: solo transformación 55; 3: solo salud 40 con
    /// previous 21) → 200 con la serie EXACTA ascendente y la forma del
    /// contrato. JWT de paciente SIN claims de permiso (convención me/*).
    /// </summary>
    [RequiresPostgresFact]
    public async Task GetScoresHistory_SerieExactaDe3Semanas_Devuelve200Ascendente()
    {
        var (patientId, _) = await CreateEnrolledPatientAsync();
        var monday = ProgramApiSeed.ThisMonday();
        await using (var db = fixture.CreateDbContext())
        {
            // Semana 1: salud + transformación fusionadas en un punto.
            await SeedHealthScoreAsync(db, patientId, monday, monday.AddDays(6), score: 21, scorePrevious: null);
            await SeedTransformationScoreAsync(db, patientId, weekNumber: 1, score: 0);
            // Semana 2: solo transformación.
            await SeedTransformationScoreAsync(db, patientId, weekNumber: 2, score: 55);
            // Semana 3: solo salud (con previous persistido).
            await SeedHealthScoreAsync(db, patientId, monday.AddDays(14), monday.AddDays(20), score: 40, scorePrevious: 21);
        }

        var client = _host.Value.CreateClient(TestJwt.Mint(Guid.NewGuid(), patientId: patientId));
        var response = await client.GetAsync("/api/v1/program/me/scores-history?weeks=12");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var points = doc.RootElement.GetProperty("points");

        Assert.Equal(3, points.GetArrayLength());

        Assert.Equal(1, points[0].GetProperty("weekNumber").GetInt32());
        Assert.Equal(monday.ToString("yyyy-MM-dd"), points[0].GetProperty("periodStart").GetString());
        Assert.Equal(monday.AddDays(6).ToString("yyyy-MM-dd"), points[0].GetProperty("periodEnd").GetString());
        Assert.Equal(21, points[0].GetProperty("healthScore").GetInt32());
        Assert.Equal(JsonValueKind.Null, points[0].GetProperty("healthPrevious").ValueKind);
        Assert.Equal(0, points[0].GetProperty("transformationScore").GetInt32());

        Assert.Equal(2, points[1].GetProperty("weekNumber").GetInt32());
        Assert.Equal(monday.AddDays(7).ToString("yyyy-MM-dd"), points[1].GetProperty("periodStart").GetString());
        Assert.Equal(monday.AddDays(13).ToString("yyyy-MM-dd"), points[1].GetProperty("periodEnd").GetString());
        Assert.Equal(JsonValueKind.Null, points[1].GetProperty("healthScore").ValueKind);
        Assert.Equal(JsonValueKind.Null, points[1].GetProperty("healthPrevious").ValueKind);
        Assert.Equal(55, points[1].GetProperty("transformationScore").GetInt32());

        Assert.Equal(3, points[2].GetProperty("weekNumber").GetInt32());
        Assert.Equal(40, points[2].GetProperty("healthScore").GetInt32());
        Assert.Equal(21, points[2].GetProperty("healthPrevious").GetInt32());
        Assert.Equal(JsonValueKind.Null, points[2].GetProperty("transformationScore").ValueKind);
    }

    /// <summary>
    /// Historial esparcido (semana 1 y semana 5 con datos, las demás vacías):
    /// solo se emiten las semanas con al menos una fila persistida — nunca
    /// huecos ni ceros inventados.
    /// </summary>
    [RequiresPostgresFact]
    public async Task GetScoresHistory_HistorialEsparcido_SoloEmiteSemanasPersistidas()
    {
        var (patientId, _) = await CreateEnrolledPatientAsync();
        var monday = ProgramApiSeed.ThisMonday();
        await using (var db = fixture.CreateDbContext())
        {
            await SeedHealthScoreAsync(db, patientId, monday, monday.AddDays(6), score: 21);
            await SeedTransformationScoreAsync(db, patientId, weekNumber: 5, score: 88);
        }

        var client = _host.Value.CreateClient(TestJwt.Mint(Guid.NewGuid(), patientId: patientId));
        var response = await client.GetAsync("/api/v1/program/me/scores-history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var points = doc.RootElement.GetProperty("points");

        Assert.Equal(2, points.GetArrayLength());
        Assert.Equal(1, points[0].GetProperty("weekNumber").GetInt32());
        Assert.Equal(5, points[1].GetProperty("weekNumber").GetInt32());
    }

    /// <summary>
    /// JWT scoping (anti-IDOR): el paciente A recibe SOLO su serie; las filas
    /// del paciente B (misma BD, mismas semanas) nunca se filtran en la
    /// respuesta de A.
    /// </summary>
    [RequiresPostgresFact]
    public async Task GetScoresHistory_JwtScoping_NuncaFiltraFilasDeOtroPaciente()
    {
        await using var db = fixture.CreateDbContext();
        var userA = await ProgramApiSeed.CreateAuthUserAsync(db);
        var userB = await ProgramApiSeed.CreateAuthUserAsync(db);
        var patientA = await ProgramApiSeed.CreatePatientAsync(db, userA, "HistorialA");
        var patientB = await ProgramApiSeed.CreatePatientAsync(db, userB, "HistorialB");
        var templateId = await ProgramApiSeed.SeedTemplateAsync(db);
        var monday = ProgramApiSeed.ThisMonday();
        await ProgramApiSeed.EnrollAsync(db, patientA, templateId, monday);
        await ProgramApiSeed.EnrollAsync(db, patientB, templateId, monday);

        await SeedHealthScoreAsync(db, patientA, monday, monday.AddDays(6), score: 21);
        await SeedTransformationScoreAsync(db, patientA, weekNumber: 1, score: 0);
        // Filas "ruidosas" del paciente B: nunca deben aparecer en la serie de A.
        await SeedHealthScoreAsync(db, patientB, monday, monday.AddDays(6), score: 99);
        await SeedTransformationScoreAsync(db, patientB, weekNumber: 2, score: 77);

        var client = _host.Value.CreateClient(TestJwt.Mint(userA, patientId: patientA));
        var response = await client.GetAsync("/api/v1/program/me/scores-history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var points = doc.RootElement.GetProperty("points");

        Assert.Equal(1, points.GetArrayLength());
        Assert.Equal(1, points[0].GetProperty("weekNumber").GetInt32());
        Assert.Equal(21, points[0].GetProperty("healthScore").GetInt32());
        Assert.Equal(0, points[0].GetProperty("transformationScore").GetInt32());
    }

    /// <summary>GET /me/scores-history sin inscripción activa → 404 NO_ACTIVE_ENROLLMENT.</summary>
    [RequiresPostgresFact]
    public async Task GetScoresHistory_SinInscripcionActiva_Devuelve404()
    {
        await using var db = fixture.CreateDbContext();
        var user = await ProgramApiSeed.CreateAuthUserAsync(db);
        var patientId = await ProgramApiSeed.CreatePatientAsync(db, user, "SinHistorial");

        var client = _host.Value.CreateClient(TestJwt.Mint(user, patientId: patientId));
        var response = await client.GetAsync("/api/v1/program/me/scores-history");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Contains("NO_ACTIVE_ENROLLMENT", doc.RootElement.GetProperty("detail").GetString());
    }

    // ------------------------------------------------------------------ setup

    /// <summary>Fila persistida de app.health_scores para una semana del paciente.</summary>
    private static async Task SeedHealthScoreAsync(
        AppDbContext db, Guid patientId, DateOnly periodStart, DateOnly periodEnd,
        int score, int? scorePrevious = null)
    {
        db.HealthScores.Add(new HealthScore
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            Score = score,
            ScorePrevious = scorePrevious,
            ScoreAdherence = score,
            ScoreClinical = score,
            ScoreNutrition = score,
            ScorePsychology = score,
            ScoreExercise = score,
            Trend = ScoreTrend.stable,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            CalculatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Fila persistida de app.transformation_scores para una semana del paciente.</summary>
    private static async Task SeedTransformationScoreAsync(
        AppDbContext db, Guid patientId, int weekNumber, int score)
    {
        db.TransformationScores.Add(new TransformationScore
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            Score = score,
            ScorePrevious = null,
            WeekNumber = weekNumber,
            Detail = JsonDocument.Parse("{}").RootElement.Clone(),
            OverallTrend = ScoreTrend.stable,
            CalculatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

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