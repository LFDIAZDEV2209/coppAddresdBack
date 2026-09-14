using System.Text.Json;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.Erp;
using CoppAddresd.Application.Features.ProgramProgress.Queries.Erp;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CoppAddresd.UnitTests.ProgramProgress.ProgramControls;

/// <summary>
/// Tests del handler de Controles del programa (UC-004,
/// <c>GET /api/v1/program/erp/patients/{patientId}/controls</c>): 404 vía null
/// sin inscripción activa, línea de tiempo de hitos (una por día configurado,
/// ascendente), control abierto vigente, próximo vencimiento, adherencia,
/// join del documento del lote de examen en UNA sola query (regresión de N+1)
/// y claves snake_case del contrato wire.
/// </summary>
public sealed class GetPatientControlsQueryHandlerTests
{
    private static readonly DateOnly Start = new(2026, 1, 5);
    private static readonly DateTime Now = new(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly IReadOnlyList<int> Days = [7, 14, 21];

    private readonly IProgramRepository _programRepository = Substitute.For<IProgramRepository>();
    private readonly IProgramControlRepository _controlRepository = Substitute.For<IProgramControlRepository>();
    private readonly IClinicalMeasurementRepository _measurementRepository =
        Substitute.For<IClinicalMeasurementRepository>();
    private readonly GetPatientControlsQueryHandler _handler;

    private readonly Guid _patientId = Guid.NewGuid();
    private readonly ProgramEnrollment _enrollment;

    /// <summary>Mediciones por lote que devuelve el repositorio (fake set-based).</summary>
    private readonly Dictionary<Guid, List<PatientMeasurementDto>> _measurementsByBatch = [];

    public GetPatientControlsQueryHandlerTests()
    {
        _enrollment = new ProgramEnrollment
        {
            Id = Guid.NewGuid(),
            PatientId = _patientId,
            Timezone = "America/Bogota",
            Status = ProgramEnrollmentStatus.Active,
            StartLocalDate = Start,
        };

        _programRepository
            .GetActiveEnrollmentForPatientAsync(_patientId, Arg.Any<CancellationToken>())
            .Returns(_enrollment);

        IReadOnlyList<ProgramControl> noControls = [];
        _controlRepository
            .ListAsync(
                enrollmentId: _enrollment.Id,
                limit: Arg.Any<int?>(),
                ct: Arg.Any<CancellationToken>())
            .Returns(noControls);

        // El repositorio real filtra por los lotes pedidos (WHERE batch_id =
        // ANY(@ids)): el fake respeta el argumento para que cada setup se
        // combine en una sola llamada sin pisarse.
        _measurementRepository
            .ListForErpAsync(
                _patientId,
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var batchIds = call.Arg<IReadOnlyCollection<Guid>>();
                return _measurementsByBatch
                    .Where(pair => batchIds.Contains(pair.Key))
                    .SelectMany(pair => pair.Value)
                    .ToList();
            });

        _handler = new GetPatientControlsQueryHandler(
            _programRepository,
            _controlRepository,
            _measurementRepository,
            Options.Create(new ProgramControlSettings { Days = [.. Days] }),
            () => Now);
    }

    // ------------------------------------------------------------ 404

    [Fact]
    public async Task Handle_SinInscripcionActiva_DevuelveNullYNoConsultaControles()
    {
        var sinInscripcion = Guid.NewGuid();
        _programRepository
            .GetActiveEnrollmentForPatientAsync(sinInscripcion, Arg.Any<CancellationToken>())
            .Returns((ProgramEnrollment?)null);

        var result = await _handler.Handle(
            new GetPatientControlsQuery(sinInscripcion), CancellationToken.None);

        // null → el controlador responde 404 (mismo contrato que el overview).
        Assert.Null(result);
        await _controlRepository.DidNotReceive().ListAsync(
            enrollmentId: Arg.Any<Guid?>(),
            limit: Arg.Any<int?>(),
            ct: Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------ línea de tiempo y agregados

    [Fact]
    public async Task Handle_ConControles_ArmaMilestonesVigenteProximoYAdherencia()
    {
        var batchId = Guid.NewGuid();
        var completed = Control(
            day: 7,
            status: ProgramControlStatus.Completed,
            examBatchId: batchId,
            sentAt: new DateTime(2026, 1, 11, 8, 0, 0, DateTimeKind.Utc),
            completedAt: new DateTime(2026, 1, 12, 9, 0, 0, DateTimeKind.Utc));
        var open = Control(
            day: 14,
            status: ProgramControlStatus.Sent,
            sentAt: new DateTime(2026, 1, 18, 8, 0, 0, DateTimeKind.Utc));
        SetControls([completed, open]);
        SetMeasurements(
            batchId,
            Measurement(batchId, "glucose_fasting", new DateTime(2026, 1, 12, 7, 0, 0, DateTimeKind.Utc), "labs/exam.pdf"),
            Measurement(batchId, "hba1c", new DateTime(2026, 1, 12, 7, 0, 0, DateTimeKind.Utc), sourceKey: null));

        var result = await _handler.Handle(
            new GetPatientControlsQuery(_patientId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(_enrollment.Id, result.Enrollment.Id);
        Assert.Equal("America/Bogota", result.Enrollment.Timezone);

        // Una entrada por día configurado, ascendente; día sin fila = pending.
        Assert.Equal(new[] { 7, 14, 21 }, result.Milestones.Select(m => m.MilestoneDay).ToArray());
        Assert.Equal("completed", result.Milestones[0].Status);
        Assert.Equal("sent", result.Milestones[1].Status);
        Assert.Equal("pending", result.Milestones[2].Status);
        Assert.Equal(completed.Id, result.Milestones[0].ControlId);
        Assert.Null(result.Milestones[2].ControlId);

        // Vigente = el abierto más reciente; próximo = menor día sin fila.
        Assert.NotNull(result.CurrentControl);
        Assert.Equal(open.Id, result.CurrentControl!.ControlId);
        Assert.Equal(14, result.CurrentControl.MilestoneDay);
        Assert.Equal("sent", result.CurrentControl.Status);
        Assert.NotNull(result.NextDue);
        Assert.Equal(21, result.NextDue!.MilestoneDay);
        Assert.Equal(Start.AddDays(20), result.NextDue.TargetDate);

        // Adherencia: 1 completado, 2 mensajes enviados, 1 día sin fila.
        Assert.Equal(1, result.Adherence.Completed);
        Assert.Equal(0, result.Adherence.Missed);
        Assert.Equal(0, result.Adherence.ClosedWithoutExam);
        Assert.Equal(1, result.Adherence.Pending);
        Assert.Equal(0, result.Adherence.Responded);
        Assert.Equal(0, result.Adherence.FollowupsSent);
        Assert.Equal(2, result.Adherence.MessagesSent);
    }

    // ------------------------------------------------------------ documento (lote de examen)

    [Fact]
    public async Task Handle_VariosControlesConLote_ConsultaTodosLosLotesEnUnaSolaQuery()
    {
        var batchA = Guid.NewGuid();
        var batchB = Guid.NewGuid();
        SetControls([
            Control(7, ProgramControlStatus.Completed, examBatchId: batchA, sentAt: Now, completedAt: Now),
            Control(14, ProgramControlStatus.Completed, examBatchId: batchB, sentAt: Now, completedAt: Now),
        ]);
        SetMeasurements(
            batchA,
            Measurement(batchA, "glucose_fasting", Now, "labs/a.pdf"));
        SetMeasurements(
            batchB,
            Measurement(batchB, "ldl", Now, "labs/b.pdf"));

        var result = await _handler.Handle(
            new GetPatientControlsQuery(_patientId), CancellationToken.None);

        // Regresión de N+1: UNA query con AMBOS lotes (una por control fallaría).
        await _measurementRepository.Received(1).ListForErpAsync(
            _patientId,
            Arg.Is<IReadOnlyCollection<Guid>>(ids =>
                ids.Count == 2 && ids.Contains(batchA) && ids.Contains(batchB)),
            Arg.Any<CancellationToken>());
        await _measurementRepository.DidNotReceive().ListForErpAsync(
            _patientId, Arg.Any<Guid?>(), Arg.Any<CancellationToken>());

        Assert.NotNull(result);
        Assert.Equal(batchA, result.Milestones[0].Document!.ExamBatchId);
        Assert.Equal(batchB, result.Milestones[1].Document!.ExamBatchId);
    }

    [Fact]
    public async Task Handle_LoteSinMedicionesYDiaPendiente_DocumentoEsNull()
    {
        var batchVacio = Guid.NewGuid();
        SetControls([
            Control(7, ProgramControlStatus.Completed, examBatchId: batchVacio, sentAt: Now, completedAt: Now),
        ]);

        var result = await _handler.Handle(
            new GetPatientControlsQuery(_patientId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Null(result.Milestones[0].Document); // lote sin mediciones
        Assert.Null(result.Milestones[2].Document); // día sin fila de control
        await _measurementRepository.Received(1).ListForErpAsync(
            _patientId,
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(batchVacio)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SinControlesConLote_NoConsultaMediciones()
    {
        SetControls([
            Control(7, ProgramControlStatus.Missed, sentAt: Now),
        ]);

        var result = await _handler.Handle(
            new GetPatientControlsQuery(_patientId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Null(result.Milestones[0].Document);
        await _measurementRepository.DidNotReceive().ListForErpAsync(
            _patientId,
            Arg.Any<IReadOnlyCollection<Guid>>(),
            Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------ contrato wire (snake_case)

    [Fact]
    public async Task Handle_RespuestaSerializada_UsaClavesSnakeCaseDelContrato()
    {
        var batchId = Guid.NewGuid();
        SetControls([
            Control(
                7,
                ProgramControlStatus.Completed,
                examBatchId: batchId,
                sentAt: new DateTime(2026, 1, 11, 8, 0, 0, DateTimeKind.Utc),
                completedAt: new DateTime(2026, 1, 12, 9, 0, 0, DateTimeKind.Utc)),
            Control(
                14,
                ProgramControlStatus.Sent,
                sentAt: new DateTime(2026, 1, 18, 8, 0, 0, DateTimeKind.Utc)),
        ]);
        SetMeasurements(
            batchId,
            Measurement(batchId, "glucose_fasting", new DateTime(2026, 1, 12, 7, 0, 0, DateTimeKind.Utc), "labs/exam.pdf"));

        var result = await _handler.Handle(
            new GetPatientControlsQuery(_patientId), CancellationToken.None);

        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("milestones", out var milestones));
        Assert.Equal(JsonValueKind.Array, milestones.ValueKind);
        Assert.True(root.TryGetProperty("current_control", out var currentControl));
        Assert.Equal("sent", currentControl.GetProperty("status").GetString());
        Assert.True(root.TryGetProperty("next_due", out var nextDue));
        Assert.Equal(21, nextDue.GetProperty("milestone_day").GetInt32());
        Assert.True(root.TryGetProperty("adherence", out var adherence));
        Assert.Equal(1, adherence.GetProperty("completed").GetInt32());
        Assert.True(adherence.TryGetProperty("closed_without_exam", out _));
        Assert.True(adherence.TryGetProperty("followups_sent", out _));
        Assert.True(adherence.TryGetProperty("messages_sent", out _));

        var milestone = milestones[0];
        Assert.True(milestone.TryGetProperty("milestone_day", out _));
        Assert.True(milestone.TryGetProperty("target_date", out _));
        Assert.True(milestone.TryGetProperty("control_id", out _));
        Assert.True(milestone.TryGetProperty("sent_at", out _));
        Assert.True(milestone.TryGetProperty("responded_at", out _));
        Assert.True(milestone.TryGetProperty("followup_sent_at", out _));
        Assert.True(milestone.TryGetProperty("completed_at", out _));
        Assert.True(milestone.TryGetProperty("closed_reason", out _));

        var document = milestone.GetProperty("document");
        Assert.True(document.TryGetProperty("exam_batch_id", out _));
        Assert.True(document.TryGetProperty("source_key", out var sourceKey));
        Assert.Equal("labs/exam.pdf", sourceKey.GetString());
        Assert.True(document.TryGetProperty("observed_at", out _));
        Assert.True(document.TryGetProperty("measurement_count", out var measurementCount));
        Assert.Equal(1, measurementCount.GetInt32());
        Assert.True(document.TryGetProperty("metric_codes", out var metricCodes));
        Assert.Equal("glucose_fasting", metricCodes[0].GetString());

        // El wire es snake_case explícito: las claves camelCase no existen.
        Assert.False(root.TryGetProperty("currentControl", out _));
        Assert.False(milestone.TryGetProperty("milestoneDay", out _));
        Assert.False(document.TryGetProperty("examBatchId", out _));
    }

    // ------------------------------------------------------------ helpers

    private void SetControls(IReadOnlyList<ProgramControl> controls) =>
        _controlRepository
            .ListAsync(
                enrollmentId: _enrollment.Id,
                limit: Arg.Any<int?>(),
                ct: Arg.Any<CancellationToken>())
            .Returns(controls);

    private void SetMeasurements(Guid batchId, params PatientMeasurementDto[] measurements) =>
        _measurementsByBatch[batchId] = [.. measurements];

    private static ProgramControl Control(
        int day,
        ProgramControlStatus status,
        Guid? examBatchId = null,
        DateTime? sentAt = null,
        DateTime? completedAt = null) => new()
    {
        Id = Guid.NewGuid(),
        EnrollmentId = Guid.NewGuid(),
        MilestoneDay = day,
        Status = status,
        ExamBatchId = examBatchId,
        SentAt = sentAt,
        CompletedAt = completedAt,
        CreatedAt = Now,
    };

    private static PatientMeasurementDto Measurement(
        Guid batchId, string metricCode, DateTime observedAt, string? sourceKey) =>
        new(
            Id: Guid.NewGuid(),
            MetricCode: metricCode,
            MetricName: metricCode,
            Value: 1m,
            UnitCode: "unit",
            UnitSymbol: "u",
            ObservedAt: observedAt,
            Source: "lab",
            BatchId: batchId,
            SourceKey: sourceKey);
}
