using System.Net;
using System.Text.Json;

namespace CoppAddresd.UnitTests.ProgramProgress.Api;

/// <summary>
/// Tests de contrato de la API del programa (T-15): afirman que el JSON
/// devuelto cumple las FORMAS de SPEC §7 (presencia de campos y tipos vía
/// <c>JsonElement</c>, no igualdad profunda frágil). Cubren AC-01 (snapshot,
/// §7.1), AC-05 (completación primera escritura, §7.2), AC-08 (calendario,
/// §7.3) y AC-09 (sendero, §7.4).
///
/// Las fechas son deterministas: la inscripción arranca el LUNES ANTERIOR al
/// lunes de la semana UTC actual, así el "hoy" del paciente (zona
/// America/Bogota, UTC-5) cae siempre dentro de la semana 2 y las fechas de
/// completación usadas (martes de la semana 1) nunca son futuras (SPEC §6.2
/// rechaza fechas futuras con 422).
/// </summary>
[Collection(ProgramApiTestCollection.Name)]
public sealed class ContractTests(ProgramApiTestDb fixture)
{
    private readonly Lazy<ProgramApiHost> _host = new(() => ProgramApiHost.Create(fixture));

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_host.IsValueCreated)
        {
            await _host.Value.DisposeAsync();
        }
    }

    /// <summary>AC-01: el snapshot (SPEC §7.1) expone los bloques y campos esperados.</summary>
    [RequiresPostgresFact]
    public async Task Snapshot_JsonShape_CumpleSpec71()
    {
        var (client, _, startMonday) = await CreateEnrolledPatientAsync();

        var response = await client.GetAsync("/api/v1/program/me/snapshot");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal(JsonValueKind.String, root.GetProperty("enrollmentId").ValueKind);

        Assert.True(root.TryGetProperty("template", out var template));
        Assert.Equal(JsonValueKind.String, template.GetProperty("id").ValueKind);
        Assert.Equal(JsonValueKind.String, template.GetProperty("code").ValueKind);
        Assert.Equal(JsonValueKind.String, template.GetProperty("name").ValueKind);
        Assert.Equal(JsonValueKind.Number, template.GetProperty("totalWeeks").ValueKind);
        Assert.Equal(JsonValueKind.Number, template.GetProperty("currentWeekNumber").ValueKind);
        Assert.Equal(JsonValueKind.String, template.GetProperty("currentWeekStatus").ValueKind);
        Assert.Equal(
            JsonValueKind.String,
            template.GetProperty("currentWeekStartDateLocal").ValueKind
        );
        Assert.Equal(
            JsonValueKind.String,
            template.GetProperty("currentWeekEndDateLocal").ValueKind
        );

        // El "hoy" del paciente (America/Bogota, UTC-5) cae siempre dentro de la
        // semana 2 de la inscripción (determinista, ver doc de la clase).
        var todayLocal = DateOnly.Parse(root.GetProperty("todayLocalDate").GetString()!);
        Assert.InRange(todayLocal, startMonday.AddDays(7), startMonday.AddDays(13));

        Assert.True(root.TryGetProperty("todayTasks", out var todayTasks));
        Assert.Equal(JsonValueKind.Array, todayTasks.ValueKind);
        Assert.Equal(6, todayTasks.GetArrayLength());
        var firstTask = todayTasks[0];
        Assert.Equal(JsonValueKind.String, firstTask.GetProperty("taskCode").ValueKind);
        Assert.Equal(JsonValueKind.String, firstTask.GetProperty("title").ValueKind);
        Assert.Equal(JsonValueKind.String, firstTask.GetProperty("short").ValueKind);
        Assert.Equal(JsonValueKind.Number, firstTask.GetProperty("points").ValueKind);
        Assert.Equal(JsonValueKind.String, firstTask.GetProperty("status").ValueKind);
        // completedAt puede ser null (tarea pendiente): el CAMPO debe existir.
        Assert.True(firstTask.TryGetProperty("completedAt", out _));
        // content puede ser null (sin media resuelto): el CAMPO debe existir.
        Assert.True(firstTask.TryGetProperty("content", out _));

        Assert.Equal(JsonValueKind.Number, root.GetProperty("todayPoints").ValueKind);
        Assert.Equal(JsonValueKind.True, root.GetProperty("todayBonusAvailable").ValueKind);
        // Máximo del día = puntos base (700) + bonus de día perfecto (50).
        Assert.Equal(750, root.GetProperty("todayPointsMax").GetInt32());

        Assert.True(root.TryGetProperty("xp", out var xp));
        Assert.Equal(JsonValueKind.Number, xp.GetProperty("balance").ValueKind);
        Assert.Equal(JsonValueKind.String, xp.GetProperty("level").ValueKind);
        Assert.Equal(JsonValueKind.Number, xp.GetProperty("nextLevelAt").ValueKind);

        Assert.True(root.TryGetProperty("streak", out var streak));
        Assert.Equal(JsonValueKind.Number, streak.GetProperty("current").ValueKind);
        Assert.Equal(JsonValueKind.Number, streak.GetProperty("longest").ValueKind);
        Assert.Equal(JsonValueKind.Number, streak.GetProperty("freezesRemaining").ValueKind);

        Assert.Equal(JsonValueKind.Number, root.GetProperty("nextMilestoneDays").ValueKind);

        Assert.True(root.TryGetProperty("calendar", out var calendar));
        Assert.Equal(JsonValueKind.Array, calendar.ValueKind);
        Assert.Equal(7, calendar.GetArrayLength());
        Assert.Equal(JsonValueKind.String, calendar[0].GetProperty("localDate").ValueKind);
        Assert.Equal(JsonValueKind.Number, calendar[0].GetProperty("weekday").ValueKind);
        Assert.Equal(JsonValueKind.False, calendar[0].GetProperty("isPerfectDay").ValueKind);
        Assert.Equal(JsonValueKind.Number, calendar[0].GetProperty("points").ValueKind);
        Assert.Equal(JsonValueKind.String, calendar[0].GetProperty("status").ValueKind);
    }

    /// <summary>
    /// AC-05 (primera escritura): el response de completar tarea (SPEC §7.2)
    /// expone los 9 campos con valores deterministas de la primera escritura
    /// (80 pts de podcast, balance 80, día no perfecto, sin bonus).
    /// </summary>
    [RequiresPostgresFact]
    public async Task CompleteTask_PrimeraEscritura_JsonShapeCumpleSpec72()
    {
        var (client, enrollmentId, startMonday) = await CreateEnrolledPatientAsync();

        var response = await client.PostAsync(
            "/api/v1/program/tasks/complete",
            ProgramApiSeed.Json(
                new
                {
                    enrollmentId,
                    localDate = startMonday.AddDays(1),
                    taskCode = "podcast",
                    clientRequestId = "ac05-1",
                }
            )
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal(JsonValueKind.String, root.GetProperty("taskCompletionId").ValueKind);
        Assert.Equal(80, root.GetProperty("pointsAwarded").GetInt32());
        Assert.Equal(80, root.GetProperty("xpBalanceAfter").GetInt32());
        Assert.Equal(JsonValueKind.False, root.GetProperty("isPerfectDay").ValueKind);
        Assert.Equal(0, root.GetProperty("dailyBonusAwarded").GetInt32());
        // Racha por umbral (SPEC §17, B): con StreakMinTasks default 1, completar
        // 1 tarea ya inicia la racha (no se requiere día perfecto).
        Assert.Equal(1, root.GetProperty("streakCurrent").GetInt32());
        Assert.Equal(0, root.GetProperty("freezesRemaining").GetInt32());
        Assert.Equal(80, root.GetProperty("dayPoints").GetInt32());
        // Máximo del día = puntos base (700) + bonus de día perfecto (50).
        Assert.Equal(750, root.GetProperty("dayPointsMax").GetInt32());
    }

    /// <summary>AC-08: el calendario (SPEC §7.3) expone ventana, días y resumen.</summary>
    [RequiresPostgresFact]
    public async Task Calendar_JsonShape_CumpleSpec73()
    {
        var (client, _, startMonday) = await CreateEnrolledPatientAsync();
        var from = startMonday;
        var to = startMonday.AddDays(6);

        var response = await client.GetAsync(
            $"/api/v1/program/calendar?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}"
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal(from.ToString("yyyy-MM-dd"), root.GetProperty("from").GetString());
        Assert.Equal(to.ToString("yyyy-MM-dd"), root.GetProperty("to").GetString());

        Assert.True(root.TryGetProperty("days", out var days));
        Assert.Equal(JsonValueKind.Array, days.ValueKind);
        Assert.Equal(7, days.GetArrayLength());
        var firstDay = days[0];
        Assert.Equal(from.ToString("yyyy-MM-dd"), firstDay.GetProperty("localDate").GetString());
        Assert.Equal(JsonValueKind.Number, firstDay.GetProperty("weekday").ValueKind);
        Assert.Equal(JsonValueKind.Number, firstDay.GetProperty("weekNumber").ValueKind);
        Assert.Equal(JsonValueKind.False, firstDay.GetProperty("isPerfectDay").ValueKind);
        Assert.Equal(JsonValueKind.Number, firstDay.GetProperty("points").ValueKind);
        Assert.Equal(JsonValueKind.Number, firstDay.GetProperty("bonusAwarded").ValueKind);
        Assert.True(firstDay.TryGetProperty("completedTaskCodes", out var codes));
        Assert.Equal(JsonValueKind.Array, codes.ValueKind);

        Assert.True(root.TryGetProperty("summary", out var summary));
        Assert.Equal(JsonValueKind.Number, summary.GetProperty("perfectDays").ValueKind);
        Assert.Equal(JsonValueKind.Number, summary.GetProperty("missedDays").ValueKind);
        Assert.Equal(JsonValueKind.Number, summary.GetProperty("totalXp").ValueKind);
    }

    /// <summary>AC-09: el sendero (SPEC §7.4) expone las 83 semanas con sus campos.</summary>
    [RequiresPostgresFact]
    public async Task Path_JsonShape_CumpleSpec74()
    {
        var (client, _, startMonday) = await CreateEnrolledPatientAsync();

        var response = await client.GetAsync("/api/v1/program/path");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("weeks", out var weeks));
        Assert.Equal(JsonValueKind.Array, weeks.ValueKind);
        Assert.Equal(83, weeks.GetArrayLength());

        var firstWeek = weeks[0];
        Assert.Equal(1, firstWeek.GetProperty("weekNumber").GetInt32());
        Assert.Equal(JsonValueKind.String, firstWeek.GetProperty("status").ValueKind);
        Assert.Equal(
            startMonday.ToString("yyyy-MM-dd"),
            firstWeek.GetProperty("weekStartDateLocal").GetString()
        );
        Assert.Equal(
            startMonday.AddDays(6).ToString("yyyy-MM-dd"),
            firstWeek.GetProperty("weekEndDateLocal").GetString()
        );
        Assert.Equal(JsonValueKind.Number, firstWeek.GetProperty("points").ValueKind);
        // isPerfectWeek es null para semanas no Completed (SPEC §7.4).
        Assert.Equal(JsonValueKind.Null, firstWeek.GetProperty("isPerfectWeek").ValueKind);

        // Los campos del resto de semanas existen (forma estable).
        var lockedWeek = weeks
            .EnumerateArray()
            .First(w => w.GetProperty("status").GetString() == "Locked");
        Assert.Equal(JsonValueKind.Null, lockedWeek.GetProperty("isPerfectWeek").ValueKind);
    }

    // ------------------------------------------------------------------ setup

    /// <summary>
    /// Paciente con usuario + plantilla de 83 semanas + inscripción activa que
    /// arranca el lunes anterior al de la semana UTC actual (fechas
    /// deterministas para las aserciones, ver doc de la clase).
    /// </summary>
    private async Task<(
        HttpClient Client,
        Guid EnrollmentId,
        DateOnly StartMonday
    )> CreateEnrolledPatientAsync()
    {
        await using var db = fixture.CreateDbContext();
        var user = await ProgramApiSeed.CreateAuthUserAsync(db);
        var patientId = await ProgramApiSeed.CreatePatientAsync(db, user, "Contrato");
        var templateId = await ProgramApiSeed.SeedTemplateAsync(db);
        var startMonday = ProgramApiSeed.ThisMonday().AddDays(-7);
        var enrollmentId = await ProgramApiSeed.EnrollAsync(db, patientId, templateId, startMonday);

        var client = _host.Value.CreateClient(TestJwt.Mint(user));
        return (client, enrollmentId, startMonday);
    }
}
