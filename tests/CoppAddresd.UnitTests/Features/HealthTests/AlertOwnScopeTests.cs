using CoppAddresd.Api.Constants;
using CoppAddresd.Api.Context;
using CoppAddresd.Api.Controllers;
using CoppAddresd.Application.Features.HealthTests;
using CoppAddresd.Application.Features.HealthTests.Alerts;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.HealthTests;

/// <summary>
/// Pruebas del alcance de datos "propios" (HealthTests.ViewOwn) en la bandeja
/// de alertas: un profesional sin HealthTests.View debe ver SOLO alertas de
/// sus pacientes asignados — el filtro de patientIds lo resuelve el servidor
/// desde la identidad del JWT (fix fuga PHI: el alcance se ignoraba y el
/// endpoint exponía alertas de todos los pacientes).
/// </summary>
public class AlertOwnScopeTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ICurrentContext _context = Substitute.For<ICurrentContext>();
    private readonly IHealthTestRepository _repository = Substitute.For<IHealthTestRepository>();

    private readonly HealthTestsController _controller;

    public AlertOwnScopeTests()
    {
        _controller = new HealthTestsController(_mediator, _context, _repository);
    }

    private static HealthTestAlert BuildAlert(Guid patientId) =>
        new()
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            Title = "Riesgo alto detectado",
            Severity = HealthTestSeverity.high,
            Status = HealthTestAlertStatus.active,
            CreatedAt = DateTime.UtcNow,
        };

    private static PaginatedHealthTestsResult<HealthTestAlertDto> Paginated(
        IEnumerable<HealthTestAlert> alerts
    )
    {
        var list = alerts.ToList();
        return new PaginatedHealthTestsResult<HealthTestAlertDto>(
            list.Select(HealthTestAlertDto.FromEntity).ToList(),
            list.Count,
            1,
            20,
            1
        );
    }

    private void SetupViewOwnScope(Guid professionalId)
    {
        _context
            .HasPermissionAsync(PermissionCodes.HealthTestsView, Arg.Any<CancellationToken>())
            .Returns(false);
        _context
            .HasPermissionAsync(PermissionCodes.HealthTestsViewOwn, Arg.Any<CancellationToken>())
            .Returns(true);
        _context.GetProfessionalIdAsync(Arg.Any<CancellationToken>()).Returns(professionalId);
    }

    [Fact]
    public async Task ListAlerts_ConAlcanceViewOwn_FiltraPorPacientesDelProfesional()
    {
        var professionalId = Guid.NewGuid();
        var ownPatientId = Guid.NewGuid();
        SetupViewOwnScope(professionalId);
        _repository
            .GetPatientIdsForProfessionalAsync(professionalId, Arg.Any<CancellationToken>())
            .Returns(new[] { ownPatientId });

        var expected = Paginated([BuildAlert(ownPatientId)]);
        ListAlertsForProfessionalQuery? sentQuery = null;
        _mediator
            .Send(
                Arg.Do<ListAlertsForProfessionalQuery>(q => sentQuery = q),
                Arg.Any<CancellationToken>()
            )
            .Returns(expected);

        var actionResult = await _controller.ListAlerts(ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var value = Assert.IsType<PaginatedHealthTestsResult<HealthTestAlertDto>>(ok.Value);
        Assert.Equal(1, value.Total);

        // El query enviado acota el alcance a los pacientes del profesional.
        Assert.NotNull(sentQuery);
        Assert.Equal([ownPatientId], sentQuery!.PatientIds);

        // Nunca se consulta la bandeja global sin filtro de alcance.
        await _mediator
            .DidNotReceive()
            .Send(Arg.Any<ListAlertsQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListAlerts_ConViewOwnYPacienteAjeno_RetornaNotFound()
    {
        var professionalId = Guid.NewGuid();
        var otherPatientId = Guid.NewGuid();
        SetupViewOwnScope(professionalId);
        _repository
            .PatientBelongsToProfessionalAsync(
                otherPatientId,
                professionalId,
                Arg.Any<CancellationToken>()
            )
            .Returns(false);

        var actionResult = await _controller.ListAlerts(
            patientId: otherPatientId,
            ct: CancellationToken.None
        );

        Assert.IsType<NotFoundObjectResult>(actionResult.Result);
        await _mediator
            .DidNotReceive()
            .Send(Arg.Any<ListAlertsQuery>(), Arg.Any<CancellationToken>());
        await _mediator
            .DidNotReceive()
            .Send(Arg.Any<ListAlertsForProfessionalQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListAlerts_ConViewOwnYPacientePropio_DevuelveAlertasDelPaciente()
    {
        var professionalId = Guid.NewGuid();
        var ownPatientId = Guid.NewGuid();
        SetupViewOwnScope(professionalId);
        _repository
            .PatientBelongsToProfessionalAsync(
                ownPatientId,
                professionalId,
                Arg.Any<CancellationToken>()
            )
            .Returns(true);

        var expected = Paginated([BuildAlert(ownPatientId)]);
        ListAlertsQuery? sentQuery = null;
        _mediator
            .Send(Arg.Do<ListAlertsQuery>(q => sentQuery = q), Arg.Any<CancellationToken>())
            .Returns(expected);

        var actionResult = await _controller.ListAlerts(
            patientId: ownPatientId,
            ct: CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.IsType<PaginatedHealthTestsResult<HealthTestAlertDto>>(ok.Value);

        // El patientId solicitado se respeta dentro del alcance propio.
        Assert.NotNull(sentQuery);
        Assert.Equal(ownPatientId, sentQuery!.PatientId);
    }

    [Fact]
    public async Task ListAlerts_ConViewGlobal_NoRestringeAlcance()
    {
        _context
            .HasPermissionAsync(PermissionCodes.HealthTestsView, Arg.Any<CancellationToken>())
            .Returns(true);

        var expected = Paginated([BuildAlert(Guid.NewGuid())]);
        ListAlertsQuery? sentQuery = null;
        _mediator
            .Send(Arg.Do<ListAlertsQuery>(q => sentQuery = q), Arg.Any<CancellationToken>())
            .Returns(expected);

        var actionResult = await _controller.ListAlerts(ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.IsType<PaginatedHealthTestsResult<HealthTestAlertDto>>(ok.Value);
        Assert.NotNull(sentQuery);

        // Un usuario global no consulta asignaciones de pacientes.
        await _repository
            .DidNotReceive()
            .GetPatientIdsForProfessionalAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handler_AlertasParaProfesional_DelegaPatientIdsAlRepositorio()
    {
        var patientIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var alert = BuildAlert(patientIds[0]);
        _repository
            .ListAlertsForProfessionalAsync(
                patientIds,
                null,
                null,
                1,
                20,
                Arg.Any<CancellationToken>()
            )
            .Returns(([alert], 1));

        var handler = new ListAlertsForProfessionalQueryHandler(_repository);

        var result = await handler.Handle(
            new ListAlertsForProfessionalQuery(patientIds),
            CancellationToken.None
        );

        Assert.Equal(1, result.Total);
        Assert.Equal(alert.Id, Assert.Single(result.Data).Id);
        await _repository
            .Received(1)
            .ListAlertsForProfessionalAsync(
                patientIds,
                null,
                null,
                1,
                20,
                Arg.Any<CancellationToken>()
            );
    }
}
