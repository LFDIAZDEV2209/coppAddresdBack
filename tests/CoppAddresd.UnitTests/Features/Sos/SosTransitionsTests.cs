using CoppAddresd.Application.Features.Sos;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.Sos;

/// <summary>
/// Pruebas adversariales del ciclo de vida SOS (task 3.2, REQ-SOS-05):
/// transiciones terminales, anti-IDOR (404 no revelador) y scopes de staff
/// (403 no revelador, sin alterar la alerta).
/// </summary>
public sealed class SosTransitionsTests
{
    private readonly ISosAlertRepository _repository = Substitute.For<ISosAlertRepository>();
    private readonly CancelSosAlertHandler _cancel;
    private readonly AttendSosAlertHandler _attend;

    public SosTransitionsTests()
    {
        _cancel = new CancelSosAlertHandler(
            _repository,
            NullLogger<CancelSosAlertHandler>.Instance
        );
        _attend = new AttendSosAlertHandler(
            _repository,
            NullLogger<AttendSosAlertHandler>.Instance
        );
    }

    private static SosAlert ActiveAlert(Guid patientId) =>
        new()
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            IdempotencyKey = Guid.NewGuid().ToString(),
            PayloadHash = "hash",
            DestinationPhoneE164 = "+573053924819",
            CreatedAt = DateTime.UtcNow,
        };

    [Fact]
    public async Task Cancel_PacienteDueno_TransicionaAListaConActorYMarca()
    {
        var patientId = Guid.NewGuid();
        var cancelledUserId = Guid.NewGuid();
        var alert = ActiveAlert(patientId);
        var cancelled = ActiveAlert(patientId);
        cancelled.Status = SosAlertStatus.Cancelada;
        cancelled.CancelledBy = cancelledUserId;
        cancelled.CancelledAt = DateTime.UtcNow;

        _repository.GetByIdAsync(alert.Id, Arg.Any<CancellationToken>()).Returns(alert, cancelled);
        _repository
            .CancelAsync(alert.Id, cancelledUserId, Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _cancel.Handle(
            new CancelSosAlertCommand(patientId, alert.Id, cancelledUserId),
            CancellationToken.None
        );

        Assert.Equal(SosTransitionOutcome.Transited, result.Outcome);
        Assert.Equal(SosAlertStatus.Cancelada.ToString(), result.Alert!.Status);
        Assert.Equal(cancelledUserId, result.Alert.CancelledBy);
        await _repository
            .Received(1)
            .CancelAsync(alert.Id, cancelledUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancel_PacienteAjeno_404NoRevelador()
    {
        // (REQ-SOS-05) IDOR: la alerta pertenece al paciente A; B intenta cancelarla.
        var alert = ActiveAlert(Guid.NewGuid());
        _repository.GetByIdAsync(alert.Id, Arg.Any<CancellationToken>()).Returns(alert);

        var result = await _cancel.Handle(
            new CancelSosAlertCommand(Guid.NewGuid(), alert.Id, Guid.NewGuid()),
            CancellationToken.None
        );

        Assert.Equal(SosTransitionOutcome.NotFound, result.Outcome);
        Assert.Null(result.Alert);

        // Nunca se altera la alerta ajena.
        await _repository
            .DidNotReceive()
            .CancelAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancel_AlertaInexistente_404NoRevelador()
    {
        _repository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((SosAlert?)null);

        var result = await _cancel.Handle(
            new CancelSosAlertCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None
        );

        Assert.Equal(SosTransitionOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task Cancel_AlertaTerminal_409()
    {
        var patientId = Guid.NewGuid();
        var alert = ActiveAlert(patientId);
        alert.Status = SosAlertStatus.Atendida;
        alert.AttendedAt = DateTime.UtcNow;
        _repository.GetByIdAsync(alert.Id, Arg.Any<CancellationToken>()).Returns(alert);

        var result = await _cancel.Handle(
            new CancelSosAlertCommand(patientId, alert.Id, Guid.NewGuid()),
            CancellationToken.None
        );

        // (REQ-SOS-05) Las transiciones son terminales: 409 inmutable.
        Assert.Equal(SosTransitionOutcome.NotActive, result.Outcome);
        await _repository
            .DidNotReceive()
            .CancelAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Attend_StaffConAsignacionDirecta_TransicionaAAtendida()
    {
        var alert = ActiveAlert(Guid.NewGuid());
        var staffUserId = Guid.NewGuid();
        var attended = ActiveAlert(alert.PatientId);
        attended.Status = SosAlertStatus.Atendida;
        attended.AttendedBy = staffUserId;
        attended.AttendedAt = DateTime.UtcNow;

        _repository.GetByIdAsync(alert.Id, Arg.Any<CancellationToken>()).Returns(alert, attended);
        _repository
            .IsStaffScopedToPatientAsync(
                alert.PatientId,
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(true);
        _repository.AttendAsync(alert.Id, staffUserId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _attend.Handle(
            new AttendSosAlertCommand(
                alert.Id,
                new SosStaffActor(staffUserId, Guid.NewGuid(), null, null, BypassScope: false)
            ),
            CancellationToken.None
        );

        Assert.Equal(SosTransitionOutcome.Transited, result.Outcome);
        Assert.Equal(SosAlertStatus.Atendida.ToString(), result.Alert!.Status);
        Assert.Equal(staffUserId, result.Alert.AttendedBy);
        Assert.NotNull(result.Alert.AttendedAt);
        await _repository
            .Received(1)
            .AttendAsync(alert.Id, staffUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Attend_StaffSinScopeClinico_403NoReveladorSinAlterar()
    {
        // (REQ-SOS-05) Staff de otra clínica: 403 no revelador, sin mutar.
        var alert = ActiveAlert(Guid.NewGuid());
        _repository.GetByIdAsync(alert.Id, Arg.Any<CancellationToken>()).Returns(alert);
        _repository
            .IsStaffScopedToPatientAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(false);

        var result = await _attend.Handle(
            new AttendSosAlertCommand(
                alert.Id,
                new SosStaffActor(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    null,
                    BypassScope: false
                )
            ),
            CancellationToken.None
        );

        Assert.Equal(SosTransitionOutcome.Forbidden, result.Outcome);
        Assert.Null(result.Alert);
        Assert.Equal(SosAlertStatus.Activa, alert.Status);
        await _repository
            .DidNotReceive()
            .AttendAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Attend_RolAdministrador_BypassDeAsignacionDirecta()
    {
        // Admin/OrgAdmin/ClinicAdmin no requieren asignación directa (D5).
        var alert = ActiveAlert(Guid.NewGuid());
        var attended = ActiveAlert(alert.PatientId);
        attended.Status = SosAlertStatus.Atendida;
        attended.AttendedAt = DateTime.UtcNow;
        _repository.GetByIdAsync(alert.Id, Arg.Any<CancellationToken>()).Returns(alert, attended);
        _repository
            .AttendAsync(alert.Id, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _attend.Handle(
            new AttendSosAlertCommand(
                alert.Id,
                new SosStaffActor(Guid.NewGuid(), null, null, null, BypassScope: true)
            ),
            CancellationToken.None
        );

        Assert.Equal(SosTransitionOutcome.Transited, result.Outcome);
        await _repository
            .DidNotReceive()
            .IsStaffScopedToPatientAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Attend_AlertaTerminal_409()
    {
        var alert = ActiveAlert(Guid.NewGuid());
        alert.Status = SosAlertStatus.Cancelada;
        _repository.GetByIdAsync(alert.Id, Arg.Any<CancellationToken>()).Returns(alert);

        var result = await _attend.Handle(
            new AttendSosAlertCommand(
                alert.Id,
                new SosStaffActor(Guid.NewGuid(), null, null, null, BypassScope: true)
            ),
            CancellationToken.None
        );

        Assert.Equal(SosTransitionOutcome.NotActive, result.Outcome);
    }

    [Fact]
    public async Task ConsultaDelPaciente_AlertaAjena_NoRevela()
    {
        var handler = new GetSosAlertHandler(_repository);
        var alert = ActiveAlert(Guid.NewGuid());
        _repository.GetByIdAsync(alert.Id, Arg.Any<CancellationToken>()).Returns(alert);

        var result = await handler.Handle(
            new GetSosAlertQuery(Guid.NewGuid(), alert.Id),
            CancellationToken.None
        );

        // (REQ-SOS-05) 404 no revelador (null en el handler).
        Assert.Null(result);
    }

    [Fact]
    public async Task ConsultaDeStaff_SinScope_NoRevela()
    {
        var handler = new GetSosAlertForStaffHandler(_repository);
        var alert = ActiveAlert(Guid.NewGuid());
        _repository.GetByIdAsync(alert.Id, Arg.Any<CancellationToken>()).Returns(alert);
        _repository
            .IsStaffScopedToPatientAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(false);

        var result = await handler.Handle(
            new GetSosAlertForStaffQuery(
                alert.Id,
                new SosStaffActor(Guid.NewGuid(), Guid.NewGuid(), null, null, BypassScope: false)
            ),
            CancellationToken.None
        );

        Assert.Equal(SosTransitionOutcome.Forbidden, result.Outcome);
        Assert.Null(result.Alert);
    }
}
