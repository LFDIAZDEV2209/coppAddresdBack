using CoppAddresd.Application.Features.Professionals;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.Professionals;

/// <summary>Transición ERP: protección del actor y confirmación durable de la proyección.</summary>
public sealed class ChangeProfessionalAccessCommandHandlerTests
{
    private readonly IEmployeeRepository _employees = Substitute.For<IEmployeeRepository>();
    private readonly IErpAccessClient _auth = Substitute.For<IErpAccessClient>();
    private readonly IProfessionalAccessProjectionRepository _projection =
        Substitute.For<IProfessionalAccessProjectionRepository>();
    private readonly Employee _employee = new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        Status = "Active",
        Professional = new Professional(),
    };

    private ChangeProfessionalAccessCommandHandler Handler =>
        new(
            _employees,
            _auth,
            _projection,
            NullLogger<ChangeProfessionalAccessCommandHandler>.Instance
        );

    public ChangeProfessionalAccessCommandHandlerTests()
    {
        _employees.GetByIdAsync(_employee.Id, Arg.Any<CancellationToken>()).Returns(_employee);
    }

    private ChangeProfessionalAccessCommand Request(
        string status = "Inactive",
        Guid? actor = null
    ) => new(_employee.Id, actor ?? Guid.NewGuid(), Guid.NewGuid(), status);

    private ErpAccessOperation ConfigureOperation(ChangeProfessionalAccessCommand request)
    {
        var operation = new ErpAccessOperation(
            request.OperationId,
            _employee.UserId!.Value,
            _employee.Id,
            request.Status,
            7,
            null
        );
        _auth
            .ChangeAsync(
                request.OperationId,
                operation.UserId,
                operation.EmployeeId,
                operation.Status,
                Arg.Any<CancellationToken>()
            )
            .Returns(operation);
        return operation;
    }

    [Fact]
    public async Task Handle_Autodesactivacion_RechazaAntesDeMutarAuth()
    {
        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            Handler.Handle(Request(actor: _employee.UserId), CancellationToken.None)
        );
        Assert.Empty(_auth.ReceivedCalls());
        Assert.Empty(_projection.ReceivedCalls());
    }

    [Theory]
    [InlineData("Invited", true, true)]
    [InlineData("Active", false, true)]
    [InlineData("Active", true, false)]
    public async Task Handle_SinProfesionalHabilitado_RechazaSinCambiarAcceso(
        string status,
        bool user,
        bool professional
    )
    {
        _employee.Status = status;
        if (!user)
            _employee.UserId = null;
        if (!professional)
            _employee.Professional = null;
        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            Handler.Handle(Request(), CancellationToken.None)
        );
        Assert.Empty(_auth.ReceivedCalls());
    }

    [Fact]
    public async Task Handle_ProyeccionFallida_MantienePendienteSinConfirmarJournal()
    {
        var request = Request();
        ConfigureOperation(request);
        _projection
            .ApplyAsync(
                _employee.Id,
                _employee.UserId!.Value,
                "Inactive",
                7,
                Arg.Any<CancellationToken>()
            )
            .Returns(Task.FromException<bool>(new HttpRequestException("No disponible")));
        var result = await Handler.Handle(request, CancellationToken.None);
        Assert.True(result.Pending);
        Assert.Equal(request.OperationId, result.OperationId);
        await _auth.DidNotReceiveWithAnyArgs().CompleteAsync(default, default);
    }

    [Fact]
    public async Task Handle_VinculoCambio_NoConfirmaJournal()
    {
        var request = Request();
        ConfigureOperation(request);
        var result = await Handler.Handle(request, CancellationToken.None);
        Assert.True(result.Pending);
        await _auth.DidNotReceiveWithAnyArgs().CompleteAsync(default, default);
    }

    [Theory]
    [InlineData("Active")]
    [InlineData("Inactive")]
    public async Task Handle_ProyeccionExitosa_ConfirmaOperacionSinActualizarCuentaGlobal(
        string status
    )
    {
        var request = Request(status);
        var operation = ConfigureOperation(request);
        _projection
            .ApplyAsync(
                operation.EmployeeId,
                operation.UserId,
                status,
                7,
                Arg.Any<CancellationToken>()
            )
            .Returns(true);
        var result = await Handler.Handle(request, CancellationToken.None);
        Assert.False(result.Pending);
        Assert.Equal(status, result.Status);
        await _auth.Received(1).CompleteAsync(operation.Id, Arg.Any<CancellationToken>());
        await _employees.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
        Assert.Equal("Active", _employee.Status);
    }

    [Fact]
    public async Task Handle_ConfirmacionFalla_ConservaOperacionPendienteParaReconciliar()
    {
        var request = Request();
        var operation = ConfigureOperation(request);
        _projection
            .ApplyAsync(
                operation.EmployeeId,
                operation.UserId,
                operation.Status,
                7,
                Arg.Any<CancellationToken>()
            )
            .Returns(true);
        _auth
            .CompleteAsync(operation.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new HttpRequestException("Respuesta perdida")));
        Assert.True((await Handler.Handle(request, CancellationToken.None)).Pending);
    }
}
