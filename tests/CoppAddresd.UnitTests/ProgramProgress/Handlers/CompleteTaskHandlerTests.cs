using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.ProgramProgress.Commands.CompleteTask;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.UnitTests.ProgramProgress.Handlers;

/// <summary>
/// Tests del caso de uso de completación de tarea (AC-01, AC-03, AC-04 y el
/// chequeo "due today" del handler). La idempotencia profunda (filas únicas,
/// XP exactamente una vez) es responsabilidad del repositorio (integración,
/// T-08); aquí se verifica la orquestación y el mapeo al shape §7.2.
/// </summary>
public class CompleteTaskHandlerTests
{
    private readonly FakeProgramRepository _repository = new();
    private readonly CompleteTaskCommandHandler _handler;

    public CompleteTaskHandlerTests()
    {
        _handler = new CompleteTaskCommandHandler(
            _repository, NullLogger<CompleteTaskCommandHandler>.Instance);
        _repository.PatientToday = new DateOnly(2026, 9, 24);
    }

    private static CompleteTaskCommand ValidCommand() => new(
        Guid.NewGuid(),
        new DateOnly(2026, 9, 24),
        TaskCode.podcast,
        "client-request-1",
        new DateTime(2026, 9, 24, 11, 14, 8, DateTimeKind.Utc),
        MoodScore: null,
        Barriers: null,
        ContentFingerprint: null);

    [Fact]
    public async Task Handle_Valido_MapeaRespuestaShape72()
    {
        var command = ValidCommand();

        var response = await _handler.Handle(command, CancellationToken.None);

        // El fake registra el resultado devuelto por CompleteTaskAsync: la
        // aserción compara el response contra ESE resultado (no re-deriva el
        // esperado del estado interno del fake, que no podía fallar).
        var result = Assert.Single(_repository.CompletedTaskResults);
        Assert.Equal(CompleteTaskOutcome.Created, result.Outcome);
        Assert.Equal(result.PointsAwarded, response.PointsAwarded);
        Assert.Equal(result.XpBalanceAfter, response.XpBalanceAfter);
        Assert.Equal(result.IsPerfectDay, response.IsPerfectDay);
        Assert.Equal(result.DailyBonusAwarded, response.DailyBonusAwarded);
        Assert.Equal(result.StreakCurrent, response.StreakCurrent);
        Assert.Equal(result.FreezesRemaining, response.FreezesRemaining);
        Assert.Equal(result.DayPoints, response.DayPoints);
        Assert.Equal(result.DayPointsMax, response.DayPointsMax);
        Assert.Equal(result.TaskCompletionId, response.TaskCompletionId);
        Assert.Equal(80, response.PointsAwarded);
        Assert.Equal(80, response.XpBalanceAfter);
        Assert.False(response.IsPerfectDay);
        Assert.Equal(0, response.DailyBonusAwarded);
        Assert.Equal(80, response.DayPoints);
        Assert.Equal(750, response.DayPointsMax);
        Assert.NotEqual(Guid.Empty, response.TaskCompletionId);
        // AC-01: el input llega al repositorio con los campos del contrato §6.2.
        var input = Assert.Single(_repository.CompletedTaskInputs);
        Assert.Equal(command.EnrollmentId, input.EnrollmentId);
        Assert.Equal(command.LocalDate, input.LocalDate);
        Assert.Equal(command.TaskCode, input.TaskCode);
        Assert.Equal(command.ClientRequestId, input.ClientRequestId);
    }

    [Fact]
    public async Task Handle_Replay_RespuestaIdenticaAPrimeraEscritura()
    {
        // AC-03: replay con otra clientRequestId → el repositorio devuelve la
        // completación existente; el body debe ser idéntico al de la primera
        // escritura (SPEC §7.2).
        var first = new CompleteTaskResult(
            CompleteTaskOutcome.Created, Guid.NewGuid(), 80, 1700, true, 50, 11, 2, 750, 750);
        var replay = first with { Outcome = CompleteTaskOutcome.Replay };

        var firstResponse = await RunWithResult(first);
        var replayResponse = await RunWithResult(replay);

        Assert.Equal(firstResponse, replayResponse);
    }

    [Fact]
    public async Task Handle_ClaveIdempotenciaReutilizada_Lanza409()
    {
        // AC-04: misma clientRequestId con otra fecha/tarea → 409.
        _repository.OnCompleteTask = (_, _) => Task.FromResult(new CompleteTaskResult(
            CompleteTaskOutcome.IdempotencyKeyReused, Guid.NewGuid(), 80, 1700, false, 0, 11, 2, 750, 750));

        var command = ValidCommand();

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("IDEMPOTENCY_KEY_REUSED", ex.Message);
    }

    [Fact]
    public async Task Handle_EmocionalSinMoodScore_RechazadoSinFabricar()
    {
        // La tarea emocional exige moodScore real: el handler nunca fabrica
        // datos clínicos (SPEC §3.10) aunque el pipeline de validación no corra.
        var command = ValidCommand() with
        {
            TaskCode = TaskCode.emocional,
            MoodScore = null,
        };

        var ex = await Assert.ThrowsAsync<UnprocessableEntityException>(
            () => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("MOOD_SCORE_REQUIRED", ex.Message);
        Assert.Empty(_repository.CompletedTaskInputs);
    }

    [Fact]
    public async Task Handle_FechaFutura_LanzaDateOutsideActiveWeek()
    {
        // Chequeo "due today" del handler (SPEC §6.1): no se aceptan fechas
        // futuras respecto al hoy local del paciente.
        var command = ValidCommand() with { LocalDate = new DateOnly(2026, 9, 25) };

        var ex = await Assert.ThrowsAsync<UnprocessableEntityException>(
            () => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("DATE_OUTSIDE_ACTIVE_WEEK", ex.Message);
        Assert.Empty(_repository.CompletedTaskInputs);
    }

    [Fact]
    public async Task Handle_HoyLocalEnZonaUTCPlus_AceptaFechaQueEnUTCesFutura()
    {
        // Zona UTC+ (p. ej. Asia/Tokyo, UTC+9): cuando en UTC aún es el día D,
        // en Tokio ya puede ser D+1. El handler valida contra el "hoy" LOCAL
        // del paciente (GetPatientLocalTodayAsync), no contra DateTime.UtcNow:
        // una fecha que es hoy local pero mañana en UTC debe aceptarse
        // (regresión del BLOCKER B4: guardias UTC rechazaban pacientes UTC+).
        var tokyoLocalToday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        _repository.PatientToday = tokyoLocalToday;

        var command = ValidCommand() with { LocalDate = tokyoLocalToday };

        var response = await _handler.Handle(command, CancellationToken.None);

        // Sin DATE_OUTSIDE_ACTIVE_WEEK: la completación llegó al repositorio.
        var input = Assert.Single(_repository.CompletedTaskInputs);
        Assert.Equal(tokyoLocalToday, input.LocalDate);
        Assert.Equal(TaskCode.podcast, input.TaskCode);
        Assert.NotEqual(Guid.Empty, response.TaskCompletionId);
    }

    [Fact]
    public async Task Handle_InscripcionInexistente_LanzaNotFound()
    {
        // Sin "hoy" del paciente no existe la inscripción → 404 NO_ACTIVE_ENROLLMENT.
        _repository.PatientToday = null;

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(ValidCommand(), CancellationToken.None));
        Assert.Contains("NO_ACTIVE_ENROLLMENT", ex.Message);
    }

    [Fact]
    public async Task Handle_VitalsConTodosLosCampos_MapeaVitalsAlInput()
    {
        // S1: el payload de signos vitales (6 campos) llega íntegro al input del
        // repositorio; el handler solo orquesta, la persistencia es del repo.
        var vitals = new VitalsPayload(72, 120, 80, 98, 130m, 70m, 36.5m, null);
        var command = ValidCommand() with
        {
            TaskCode = TaskCode.vitals,
            Vitals = vitals,
        };

        await _handler.Handle(command, CancellationToken.None);

        var input = Assert.Single(_repository.CompletedTaskInputs);
        Assert.NotNull(input.Vitals);
        Assert.Equal(72, input.Vitals!.HeartRate);
        Assert.Equal(120, input.Vitals.Systolic);
        Assert.Equal(80, input.Vitals.Diastolic);
        Assert.Equal(98, input.Vitals.O2Saturation);
        Assert.Equal(130m, input.Vitals.Glucose);
        Assert.Equal(70m, input.Vitals.WeightKg);
        Assert.Equal(36.5m, input.Vitals.TemperatureC);
    }

    [Fact]
    public async Task Handle_TareaNoVitals_SinPayload_InputVitalsEsNull()
    {
        // S2: tarea no-vitals (podcast) sin payload → el input no trae Vitals
        // (backwards compatible; el repositorio no crea mediciones).
        var command = ValidCommand();

        await _handler.Handle(command, CancellationToken.None);

        var input = Assert.Single(_repository.CompletedTaskInputs);
        Assert.Null(input.Vitals);
        Assert.Equal(TaskCode.podcast, input.TaskCode);
    }

    private async Task<CompleteTaskResponseDto> RunWithResult(CompleteTaskResult result)
    {
        _repository.OnCompleteTask = (_, _) => Task.FromResult(result);
        return await _handler.Handle(ValidCommand(), CancellationToken.None);
    }
}