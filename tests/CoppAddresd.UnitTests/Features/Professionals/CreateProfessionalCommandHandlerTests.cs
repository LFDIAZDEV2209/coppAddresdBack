using CoppAddresd.Application.Features.Professionals;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.Professionals;

public class CreateProfessionalCommandHandlerTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IEmployeeRepository _employeeRepository = Substitute.For<IEmployeeRepository>();
    private readonly IAuthInvitationsClient _auth = Substitute.For<IAuthInvitationsClient>();
    private readonly IAuthScopedAssignmentsClient _scoped = Substitute.For<IAuthScopedAssignmentsClient>();
    private readonly ILogger<CreateProfessionalCommandHandler> _logger =
        Substitute.For<ILogger<CreateProfessionalCommandHandler>>();

    private CreateProfessionalCommandHandler CreateHandler()
        => new(_mediator, _employeeRepository, _auth, _scoped, _logger);

    private static CreateProfessionalCommand ComandoBase(Guid clinicId, bool sendInvitation = true)
    {
        var organizationId = Guid.NewGuid();
        return new CreateProfessionalCommand(
            organizationId,
            "Ana",
            null,
            "López",
            "ana@mediquer.com",
            null,
            null,
            null,
            null,
            Guid.NewGuid(),
            null,
            [new ClinicAssignmentInput(clinicId, true, "Active")],
            [],
            [],
            [new ScopedRoleAssignmentInput(Guid.NewGuid(), "Clinic", clinicId)],
            [],
            sendInvitation,
            null);
    }

    private static EmployeeDto EmployeeDto(Guid id) => new(
        id,
        Guid.NewGuid(),
        "MediQuer",
        null,
        "Ana",
        null,
        "López",
        "ana@mediquer.com",
        null,
        null,
        null,
        null,
        null,
        "Invited",
        DateTime.UtcNow,
        null,
        [],
        null);

    [Fact]
    public async Task Handle_ScopesSinInvitacion_LanzaBusinessRule()
    {
        var handler = CreateHandler();
        var command = ComandoBase(Guid.NewGuid(), sendInvitation: false);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            handler.Handle(command, CancellationToken.None));

        await _auth.DidNotReceiveWithAnyArgs().CreateInvitationAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Handle_ConInvitacionYScopes_AplicaScopesYVinculaUsuario()
    {
        var clinicId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var invitationId = Guid.NewGuid();

        _mediator.Send(Arg.Any<CreateEmployeeCommand>(), Arg.Any<CancellationToken>())
            .Returns(EmployeeDto(employeeId));

        _auth.CreateInvitationAsync("ana@mediquer.com", "Ana", "López", Arg.Any<CancellationToken>())
            .Returns(new InvitationCreationResult(userId, invitationId, DateTime.UtcNow.AddHours(72), "http://link"));

        var handler = CreateHandler();

        var result = await handler.Handle(ComandoBase(clinicId), CancellationToken.None);

        Assert.Equal(employeeId, result.EmployeeId);
        Assert.Equal(invitationId, result.InvitationId);
        Assert.NotNull(result.InvitationLink);

        // El usuario se vincula al empleado y los scopes se aplican de forma atómica.
        await _employeeRepository.Received(1).SetUserIdAsync(employeeId, userId, Arg.Any<CancellationToken>());
        await _scoped.Received(1).ReplaceAsync(
            userId,
            Arg.Any<IReadOnlyList<ScopedRoleAssignmentInput>>(),
            Arg.Any<IReadOnlyList<ScopedPermissionAssignmentInput>>(),
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FallaAplicacionDeScopes_CompensaRevocandoYEliminando()
    {
        var clinicId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var invitationId = Guid.NewGuid();

        _mediator.Send(Arg.Any<CreateEmployeeCommand>(), Arg.Any<CancellationToken>())
            .Returns(EmployeeDto(employeeId));

        _auth.CreateInvitationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new InvitationCreationResult(userId, invitationId, DateTime.UtcNow.AddHours(72), "http://link"));

        _scoped.ReplaceAsync(
                Arg.Any<Guid>(),
                Arg.Any<IReadOnlyList<ScopedRoleAssignmentInput>>(),
                Arg.Any<IReadOnlyList<ScopedPermissionAssignmentInput>>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new UnprocessableEntityException("Rol no encontrado")));

        var handler = CreateHandler();

        await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            handler.Handle(ComandoBase(clinicId), CancellationToken.None));

        // Compensación: se revoca la invitación y se elimina el empleado recién creado.
        await _auth.Received(1).RevokeAsync(invitationId, Arg.Any<CancellationToken>());
        await _employeeRepository.Received(1).DeleteAsync(employeeId, Arg.Any<CancellationToken>());
    }
}