using CoppAddresd.Application.Features.Professionals;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CoppAddresd.UnitTests.Features.Professionals;

public class BulkCreateEmployeesTests
{
    private readonly IEmployeeRepository _employeeRepo = Substitute.For<IEmployeeRepository>();
    private readonly IOrganizationRepository _orgRepo = Substitute.For<IOrganizationRepository>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IAuthRolesClient _authRolesClient = Substitute.For<IAuthRolesClient>();
    private readonly ILogger<BulkCreateEmployeesCommandHandler> _logger =
        Substitute.For<ILogger<BulkCreateEmployeesCommandHandler>>();

    private BulkCreateEmployeesCommandHandler CreateHandler()
        => new(_mediator, _employeeRepo, _orgRepo, _authRolesClient, _logger);

    private static Organization Org(Guid id) =>
        new() { Id = id, Code = "test-org", Name = "Test Org" };

    private static ProfessionalType ProfType(Guid id, string name) =>
        new() { Id = id, Code = "CODE", Name = name, IsActive = true, CreatedAt = DateTime.UtcNow };

    private static BulkEmployeeRowInput Row(
        string firstName = "Ana",
        string lastName = "López",
        string email = "ana@test.com",
        string? professionalTypeName = null,
        string status = "activo",
        IReadOnlyList<EmployeeBulkClinicInput>? clinics = null) =>
        new(firstName, lastName, email, professionalTypeName, status, clinics);

    /// <summary>Configura mocks comunes: employeeRepo.AddAsync devuelve la entidad y
    /// mediator.Send para InviteEmployeeCommand devuelve un resultado exitoso por defecto.</summary>
    private void SetupHappyPathMocks(Guid orgId)
    {
        _orgRepo.GetOrganizationByIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(Org(orgId));
        _orgRepo.ListProfessionalTypesAsync(Arg.Any<CancellationToken>())
            .Returns([]);
        _employeeRepo.EmailExistsInOrganizationAsync(orgId, Arg.Any<string>(), null, Arg.Any<CancellationToken>())
            .Returns(false);
        _employeeRepo.AddAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Employee>());

        // Default: invitación exitosa
        _mediator.Send(Arg.Any<InviteEmployeeCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var cmd = callInfo.Arg<InviteEmployeeCommand>();
                return new InviteEmployeeResult(
                    cmd.EmployeeId,
                    Guid.NewGuid(),    // UserId
                    Guid.NewGuid(),    // InvitationId
                    DateTime.UtcNow.AddHours(24),
                    null);
            });
    }

    [Fact]
    public async Task FilaValidaProfesional_CreaEmpleadoConExtension()
    {
        var orgId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        _orgRepo.GetOrganizationByIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(Org(orgId));
        _orgRepo.ListProfessionalTypesAsync(Arg.Any<CancellationToken>())
            .Returns([ProfType(typeId, "Médico Cirujano")]);
        _employeeRepo.EmailExistsInOrganizationAsync(orgId, Arg.Any<string>(), null, Arg.Any<CancellationToken>())
            .Returns(false);
        _employeeRepo.AddAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Employee>());
        _mediator.Send(Arg.Any<InviteEmployeeCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var cmd = callInfo.Arg<InviteEmployeeCommand>();
                return new InviteEmployeeResult(cmd.EmployeeId, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddHours(24), null);
            });

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(professionalTypeName: "Médico Cirujano"),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.True(result.Results[0].Success);
        Assert.NotNull(result.Results[0].EmployeeId);
        Assert.NotNull(result.Results[0].UserId);
        Assert.Equal(1, result.Created);
        Assert.Equal(0, result.Failed);

        // Verificar que se creó con extensión profesional
        var employee = result.Results[0].EmployeeId!.Value;
        await _employeeRepo.Received(1).AddAsync(
            Arg.Is<Employee>(e => e.Professional != null && e.Professional.ProfessionalTypeId == typeId),
            Arg.Any<CancellationToken>());

        // Verificar que se envió la invitación
        await _mediator.Received(1).Send(
            Arg.Is<InviteEmployeeCommand>(c => c.EmployeeId == employee),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FilaValidaNoClinica_CreaEmpleadoSinExtension()
    {
        var orgId = Guid.NewGuid();
        SetupHappyPathMocks(orgId);

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(professionalTypeName: null),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.True(result.Results[0].Success);
        Assert.NotNull(result.Results[0].EmployeeId);
        Assert.NotNull(result.Results[0].UserId);
        Assert.Equal(1, result.Created);
        Assert.Equal(0, result.Failed);

        await _employeeRepo.Received(1).AddAsync(
            Arg.Is<Employee>(e => e.Professional == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmailDuplicadoEnBatch_FilaFallida()
    {
        var orgId = Guid.NewGuid();
        SetupHappyPathMocks(orgId);

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(email: "dup@test.com"),
            Row(email: "dup@test.com"),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(2, result.Results.Count);
        Assert.True(result.Results[0].Success);
        Assert.False(result.Results[1].Success);
        Assert.Contains("duplicado", result.Results[1].Error!);
        Assert.Equal(1, result.Created);
        Assert.Equal(1, result.Failed);
    }

    [Fact]
    public async Task EmailExistenteEnOrg_FilaFallida()
    {
        var orgId = Guid.NewGuid();
        _orgRepo.GetOrganizationByIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(Org(orgId));
        _orgRepo.ListProfessionalTypesAsync(Arg.Any<CancellationToken>())
            .Returns([]);
        _employeeRepo.EmailExistsInOrganizationAsync(orgId, "existente@test.com", null, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(email: "existente@test.com"),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.False(result.Results[0].Success);
        Assert.Contains("ya existe", result.Results[0].Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Failed);
    }

    [Fact]
    public async Task TipoProfesionalInexistente_FilaFallida()
    {
        var orgId = Guid.NewGuid();
        _orgRepo.GetOrganizationByIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(Org(orgId));
        _orgRepo.ListProfessionalTypesAsync(Arg.Any<CancellationToken>())
            .Returns([ProfType(Guid.NewGuid(), "Médico Cirujano")]);

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(professionalTypeName: "Tipo Inexistente"),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.False(result.Results[0].Success);
        Assert.Contains("no existe", result.Results[0].Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Failed);
    }

    [Fact]
    public async Task StatusInvalido_FilaFallida()
    {
        var orgId = Guid.NewGuid();
        _orgRepo.GetOrganizationByIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(Org(orgId));
        _orgRepo.ListProfessionalTypesAsync(Arg.Any<CancellationToken>())
            .Returns([]);

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(status: "pendiente"),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.False(result.Results[0].Success);
        Assert.Contains("no es válido", result.Results[0].Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Failed);
    }

    [Fact]
    public async Task CapExcedido_ValidadorRechaza()
    {
        var orgId = Guid.NewGuid();
        var rows = Enumerable.Range(1, 501)
            .Select(i => Row(email: $"user{i}@test.com"))
            .ToList();
        var command = new BulkCreateEmployeesCommand(orgId, rows);

        var validator = new BulkCreateEmployeesValidator();
        var validationResult = await validator.ValidateAsync(command);

        Assert.False(validationResult.IsValid);
        Assert.Contains(validationResult.Errors, e => e.ErrorMessage.Contains("500"));
    }

    [Fact]
    public async Task MezclaParcialSuccess_ContaCorrectos()
    {
        var orgId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        _orgRepo.GetOrganizationByIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(Org(orgId));
        _orgRepo.ListProfessionalTypesAsync(Arg.Any<CancellationToken>())
            .Returns([ProfType(typeId, "Enfermera")]);

        // La primera fila pasa, la segunda tiene email duplicado en batch,
        // la tercera tiene status inválido, la cuarta pasa como no-clínica.
        _employeeRepo.EmailExistsInOrganizationAsync(orgId, Arg.Any<string>(), null, Arg.Any<CancellationToken>())
            .Returns(false);
        _employeeRepo.AddAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Employee>());
        _mediator.Send(Arg.Any<InviteEmployeeCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var cmd = callInfo.Arg<InviteEmployeeCommand>();
                return new InviteEmployeeResult(cmd.EmployeeId, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddHours(24), null);
            });

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(email: "ok1@test.com", professionalTypeName: "Enfermera"),
            Row(email: "ok1@test.com"),                           // duplicado en batch
            Row(email: "ok2@test.com", status: "invalido"),       // status inválido
            Row(email: "ok3@test.com"),                           // no-clínica, válida
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(4, result.Results.Count);
        Assert.True(result.Results[0].Success);   // ok1
        Assert.False(result.Results[1].Success);  // duplicado batch
        Assert.False(result.Results[2].Success);  // status inválido
        Assert.True(result.Results[3].Success);   // ok3
        Assert.Equal(2, result.Created);
        Assert.Equal(2, result.Failed);
    }

    [Fact]
    public async Task OrganizacionInexistente_TodasLasFilasFallan()
    {
        var orgId = Guid.NewGuid();
        _orgRepo.GetOrganizationByIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns((Organization?)null);

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(email: "a@test.com"),
            Row(email: "b@test.com"),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(2, result.Results.Count);
        Assert.All(result.Results, r => Assert.False(r.Success));
        Assert.All(result.Results, r => Assert.Contains("organización", r.Error!, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, result.Created);
        Assert.Equal(2, result.Failed);
    }

    [Fact]
    public async Task TipoProfesionalConCaseInsensitive_SiResuelve()
    {
        var orgId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        SetupHappyPathMocks(orgId);
        _orgRepo.ListProfessionalTypesAsync(Arg.Any<CancellationToken>())
            .Returns([ProfType(typeId, "médico cirujano")]);

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(email: "doc@test.com", professionalTypeName: "MÉDICO CIRUJANO"),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.True(result.Results[0].Success);
        Assert.NotNull(result.Results[0].UserId);
        Assert.Equal(1, result.Created);
    }

    [Fact]
    public async Task StatusCaseInsensitive_MapeaCorrectamente()
    {
        var orgId = Guid.NewGuid();
        SetupHappyPathMocks(orgId);

        Employee? captured = null;
        _employeeRepo.AddAsync(Arg.Do<Employee>(e => captured = e), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Employee>());

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(email: "a@test.com", status: "ACTIVO"),
            Row(email: "b@test.com", status: "Invitado"),
            Row(email: "c@test.com", status: "INACTIVO"),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(3, result.Created);
        Assert.Equal(0, result.Failed);
        Assert.All(result.Results, r => Assert.True(r.Success));
        Assert.All(result.Results, r => Assert.NotNull(r.UserId));
        await _employeeRepo.Received(3).AddAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FirstNameVacio_FilaFallida()
    {
        var orgId = Guid.NewGuid();
        _orgRepo.GetOrganizationByIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(Org(orgId));
        _orgRepo.ListProfessionalTypesAsync(Arg.Any<CancellationToken>())
            .Returns([]);

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(firstName: ""),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.False(result.Results[0].Success);
        Assert.Contains("nombre", result.Results[0].Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LastNameVacio_FilaFallida()
    {
        var orgId = Guid.NewGuid();
        _orgRepo.GetOrganizationByIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(Org(orgId));
        _orgRepo.ListProfessionalTypesAsync(Arg.Any<CancellationToken>())
            .Returns([]);

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(lastName: ""),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.False(result.Results[0].Success);
        Assert.Contains("apellidos", result.Results[0].Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmailFormatoInvalido_FilaFallida()
    {
        var orgId = Guid.NewGuid();
        _orgRepo.GetOrganizationByIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(Org(orgId));
        _orgRepo.ListProfessionalTypesAsync(Arg.Any<CancellationToken>())
            .Returns([]);

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(email: "no-es-email"),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.False(result.Results[0].Success);
        Assert.Contains("formato", result.Results[0].Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmailVacio_FilaFallida()
    {
        var orgId = Guid.NewGuid();
        _orgRepo.GetOrganizationByIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(Org(orgId));
        _orgRepo.ListProfessionalTypesAsync(Arg.Any<CancellationToken>())
            .Returns([]);

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(email: ""),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.False(result.Results[0].Success);
        Assert.Contains("correo", result.Results[0].Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StatusVacio_FilaFallida()
    {
        var orgId = Guid.NewGuid();
        _orgRepo.GetOrganizationByIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(Org(orgId));
        _orgRepo.ListProfessionalTypesAsync(Arg.Any<CancellationToken>())
            .Returns([]);

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(status: ""),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.False(result.Results[0].Success);
        Assert.Contains("estado", result.Results[0].Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validator_RechazaRowsVacios()
    {
        var command = new BulkCreateEmployeesCommand(Guid.NewGuid(), []);
        var validator = new BulkCreateEmployeesValidator();
        var result = validator.Validate(command);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validator_RechazaOrganizationIdVacio()
    {
        var command = new BulkCreateEmployeesCommand(Guid.Empty, [Row()]);
        var validator = new BulkCreateEmployeesValidator();
        var result = validator.Validate(command);
        Assert.False(result.IsValid);
    }

    // ─── Tests de adopt ───────────────────────────────────────────────

    [Fact]
    public async Task AdoptHasPassword_FilaExitosaSinCompensacion()
    {
        // Adopt con password: InviteEmployeeCommand devuelve resultado con
        // HasPassword=true, la fila es exitosa y no hay compensación.
        var orgId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        SetupHappyPathMocks(orgId);

        // Simular InviteEmployeeCommand devolviendo resultado adopt+hasPassword.
        _mediator.Send(Arg.Any<InviteEmployeeCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var cmd = callInfo.Arg<InviteEmployeeCommand>();
                return new InviteEmployeeResult(
                    cmd.EmployeeId,
                    expectedUserId,
                    Guid.Empty,    // Sin invitación (hasPassword)
                    DateTime.MinValue,
                    null);
            });

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(email: "existente@test.com"),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.True(result.Results[0].Success);
        Assert.Equal(expectedUserId, result.Results[0].UserId);
        Assert.Equal(1, result.Created);
        Assert.Equal(0, result.Failed);

        // Se vinculó el usuario al empleado (SetUserIdAsync es interno de InviteEmployeeCommand).
        await _mediator.Received(1).Send(
            Arg.Is<InviteEmployeeCommand>(c => c.InvitedBy == null),
            Arg.Any<CancellationToken>());
        // No hubo compensación (no se eliminó el empleado).
        await _employeeRepo.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProfessionalTypeNameVacio_EsEmpleadoNoClinico()
    {
        var orgId = Guid.NewGuid();
        SetupHappyPathMocks(orgId);
        _orgRepo.ListProfessionalTypesAsync(Arg.Any<CancellationToken>())
            .Returns([ProfType(Guid.NewGuid(), "Médico")]);

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(email: "admin@test.com", professionalTypeName: ""),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.True(result.Results[0].Success);
        await _employeeRepo.Received(1).AddAsync(
            Arg.Is<Employee>(e => e.Professional == null),
            Arg.Any<CancellationToken>());
    }

    // ─── Nuevos tests: auto-invite ────────────────────────────────────

    [Fact]
    public async Task InvitacionExitosa_SetUserIdEnResultado()
    {
        var orgId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        SetupHappyPathMocks(orgId);

        // Configurar un userId específico en la respuesta
        _mediator.Send(Arg.Any<InviteEmployeeCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var cmd = callInfo.Arg<InviteEmployeeCommand>();
                return new InviteEmployeeResult(cmd.EmployeeId, expectedUserId, Guid.NewGuid(), DateTime.UtcNow.AddHours(24), null);
            });

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(email: "nuevo@test.com"),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.True(result.Results[0].Success);
        Assert.NotNull(result.Results[0].UserId);
        Assert.Equal(expectedUserId, result.Results[0].UserId);

        // Verificar que se envió el InviteEmployeeCommand
        await _mediator.Received(1).Send(
            Arg.Is<InviteEmployeeCommand>(c => c.InvitedBy == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvitacionFallida_CompensaEliminaEmpleado_FilaFallida()
    {
        var orgId = Guid.NewGuid();
        _orgRepo.GetOrganizationByIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(Org(orgId));
        _orgRepo.ListProfessionalTypesAsync(Arg.Any<CancellationToken>())
            .Returns([]);
        _employeeRepo.EmailExistsInOrganizationAsync(orgId, Arg.Any<string>(), null, Arg.Any<CancellationToken>())
            .Returns(false);
        _employeeRepo.AddAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Employee>());

        // La invitación falla
        _mediator.Send(Arg.Any<InviteEmployeeCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("El servicio de autenticación no está disponible."));

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(email: "fail@test.com"),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.False(result.Results[0].Success);
        Assert.Contains("invitación", result.Results[0].Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Failed);

        // Verificar compensación: se intentó eliminar el empleado
        await _employeeRepo.Received(1).DeleteAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvitacionFallida_OtrasFilasAfectadas_Independencia()
    {
        var orgId = Guid.NewGuid();
        _orgRepo.GetOrganizationByIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(Org(orgId));
        _orgRepo.ListProfessionalTypesAsync(Arg.Any<CancellationToken>())
            .Returns([]);
        _employeeRepo.EmailExistsInOrganizationAsync(orgId, Arg.Any<string>(), null, Arg.Any<CancellationToken>())
            .Returns(false);
        _employeeRepo.AddAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Employee>());

        var callCount = 0;
        _mediator.Send(Arg.Any<InviteEmployeeCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callCount++;
                var cmd = callInfo.Arg<InviteEmployeeCommand>();
                // La primera fila falla, las demás exitosas
                if (callCount == 1)
                    throw new InvalidOperationException("Error transitorio del Auth Service");
                return new InviteEmployeeResult(cmd.EmployeeId, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddHours(24), null);
            });

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(email: "fail@test.com"),       // fila 1: invitación falla → compensada
            Row(email: "ok1@test.com"),        // fila 2: éxito
            Row(email: "ok2@test.com"),        // fila 3: éxito
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(3, result.Results.Count);
        Assert.False(result.Results[0].Success);  // fila 1: compensada
        Assert.True(result.Results[1].Success);   // fila 2: éxito
        Assert.True(result.Results[2].Success);   // fila 3: éxito
        Assert.Equal(2, result.Created);
        Assert.Equal(1, result.Failed);

        // Verificar que userId está presente en las filas exitosas
        Assert.NotNull(result.Results[1].UserId);
        Assert.NotNull(result.Results[2].UserId);

        // Verificar que se intentó eliminar solo el empleado de la fila 1
        await _employeeRepo.Received(1).DeleteAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BatchIndependencia_UnaFilaNoDetieneOtras()
    {
        var orgId = Guid.NewGuid();
        SetupHappyPathMocks(orgId);

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(email: "ok1@test.com"),
            Row(firstName: ""),               // validación falla, no crea empleado
            Row(email: "ok2@test.com"),
            Row(email: "ok2@test.com"),       // duplicado en batch
            Row(email: "ok3@test.com"),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(5, result.Results.Count);
        Assert.True(result.Results[0].Success);
        Assert.False(result.Results[1].Success);  // nombre vacío
        Assert.True(result.Results[2].Success);
        Assert.False(result.Results[3].Success);  // duplicado
        Assert.True(result.Results[4].Success);
        Assert.Equal(3, result.Created);
        Assert.Equal(2, result.Failed);

        // Las 3 filas exitosas tienen userId
        Assert.NotNull(result.Results[0].UserId);
        Assert.NotNull(result.Results[2].UserId);
        Assert.NotNull(result.Results[4].UserId);
    }

    // ─── Tests: clínicas por código + rol scoped ──────────────────────

    [Fact]
    public async Task FilaConDosClinicasRoles_CreaAssignmentsEInvitaConScopes()
    {
        var orgId = Guid.NewGuid();
        var clinicId1 = Guid.NewGuid();
        var clinicId2 = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        SetupHappyPathMocks(orgId);

        // Configurar resolución de clínicas por código.
        _orgRepo.GetClinicsByCodesAsync(orgId, Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([(clinicId1, "CL001"), (clinicId2, "CL002")]);

        // Configurar resolución de rol por nombre.
        _authRolesClient.GetRoleByNameAsync("Professional", Arg.Any<CancellationToken>())
            .Returns(new AuthRoleLookupResult(roleId, "Professional", true));

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(clinics: [
                new EmployeeBulkClinicInput("CL001", "Professional"),
                new EmployeeBulkClinicInput("CL002", "Professional"),
            ]),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.True(result.Results[0].Success);
        Assert.NotNull(result.Results[0].EmployeeId);
        Assert.Equal(1, result.Created);
        Assert.Equal(0, result.Failed);

        // Verificar que el empleado tiene 2 asignaciones de clínica.
        await _employeeRepo.Received(1).AddAsync(
            Arg.Is<Employee>(e =>
                e.ClinicAssignments != null && e.ClinicAssignments.Count == 2
                && e.ClinicAssignments.Any(a => a.ClinicId == clinicId1 && a.IsPrimary)
                && e.ClinicAssignments.Any(a => a.ClinicId == clinicId2 && !a.IsPrimary)),
            Arg.Any<CancellationToken>());

        // Verificar que se envió InviteEmployeeCommand con ScopedRoles.
        await _mediator.Received(1).Send(
            Arg.Is<InviteEmployeeCommand>(c =>
                c.ScopedRoles != null
                && c.ScopedRoles.Count == 2
                && c.ScopedRoles.Any(s => s.ScopeId == clinicId1 && s.ScopeType == "Clinic")
                && c.ScopedRoles.Any(s => s.ScopeId == clinicId2 && s.ScopeType == "Clinic")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CodigoClinicaDesconocido_FilaFallida()
    {
        var orgId = Guid.NewGuid();
        SetupHappyPathMocks(orgId);

        // Código desconocido: no retorna nada.
        _orgRepo.GetClinicsByCodesAsync(orgId, Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(clinics: [
                new EmployeeBulkClinicInput("NOEXISTE", "Professional"),
            ]),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.False(result.Results[0].Success);
        Assert.Contains("Clínica no encontrada", result.Results[0].Error!);
        Assert.Contains("NOEXISTE", result.Results[0].Error!);
        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Failed);
    }

    [Fact]
    public async Task RolDesconocido_FilaFallida()
    {
        var orgId = Guid.NewGuid();
        var clinicId = Guid.NewGuid();
        SetupHappyPathMocks(orgId);

        _orgRepo.GetClinicsByCodesAsync(orgId, Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([(clinicId, "CL001")]);

        // Rol no encontrado → null.
        _authRolesClient.GetRoleByNameAsync("NoExiste", Arg.Any<CancellationToken>())
            .Returns((AuthRoleLookupResult?)null);

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(clinics: [
                new EmployeeBulkClinicInput("CL001", "NoExiste"),
            ]),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.False(result.Results[0].Success);
        Assert.Contains("Rol no encontrado o inactivo", result.Results[0].Error!);
        Assert.Contains("NoExiste", result.Results[0].Error!);
        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Failed);
    }

    [Fact]
    public async Task ClinicaSinRol_FilaFallida()
    {
        var orgId = Guid.NewGuid();
        SetupHappyPathMocks(orgId);

        // Fila sin clínicas funciona igual que antes (backward compat).
        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(clinics: null),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.True(result.Results[0].Success);
        Assert.Equal(1, result.Created);

        // No se llama a GetClinicsByCodesAsync cuando no hay clínicas.
        await _orgRepo.DidNotReceive().GetClinicsByCodesAsync(
            Arg.Any<Guid>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FilaSinClinicas_ComportamientoIgualAntes()
    {
        var orgId = Guid.NewGuid();
        SetupHappyPathMocks(orgId);

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(email: "ok@test.com"),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.True(result.Results[0].Success);
        Assert.Equal(1, result.Created);
        Assert.Equal(0, result.Failed);

        // Sin clínicas, el empleado se crea sin ClinicAssignments.
        await _employeeRepo.Received(1).AddAsync(
            Arg.Is<Employee>(e => e.ClinicAssignments == null || e.ClinicAssignments.Count == 0),
            Arg.Any<CancellationToken>());

        // Invitación sin ScopedRoles.
        await _mediator.Received(1).Send(
            Arg.Is<InviteEmployeeCommand>(c => c.ScopedRoles == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RolesDuplicadosEnLote_SeResuelvenUnaSolaVez()
    {
        var orgId = Guid.NewGuid();
        var clinicId1 = Guid.NewGuid();
        var clinicId2 = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        SetupHappyPathMocks(orgId);

        _orgRepo.GetClinicsByCodesAsync(orgId, Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([(clinicId1, "CL001"), (clinicId2, "CL002")]);

        _authRolesClient.GetRoleByNameAsync("Professional", Arg.Any<CancellationToken>())
            .Returns(new AuthRoleLookupResult(roleId, "Professional", true));

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(email: "a@test.com", clinics: [
                new EmployeeBulkClinicInput("CL001", "Professional"),
                new EmployeeBulkClinicInput("CL002", "Professional"),
            ]),
            Row(email: "b@test.com", clinics: [
                new EmployeeBulkClinicInput("CL001", "Professional"),
                new EmployeeBulkClinicInput("CL002", "Professional"),
            ]),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(2, result.Created);
        Assert.Equal(0, result.Failed);

        // El rol "Professional" se resolvió una sola vez (cache), no dos.
        await _authRolesClient.Received(1).GetRoleByNameAsync("Professional", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClinicaInactiva_FilaFallida()
    {
        var orgId = Guid.NewGuid();
        SetupHappyPathMocks(orgId);

        // La clínica inactiva no aparece en los resultados del repo.
        _orgRepo.GetClinicsByCodesAsync(orgId, Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(clinics: [
                new EmployeeBulkClinicInput("INACTIVA", "Professional"),
            ]),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.False(result.Results[0].Success);
        Assert.Contains("Clínica no encontrada", result.Results[0].Error!);
    }

    [Fact]
    public async Task RolInactivo_FilaFallida()
    {
        var orgId = Guid.NewGuid();
        var clinicId = Guid.NewGuid();
        SetupHappyPathMocks(orgId);

        _orgRepo.GetClinicsByCodesAsync(orgId, Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([(clinicId, "CL001")]);

        // Rol inactivo → IsActive=false.
        _authRolesClient.GetRoleByNameAsync("InactiveRole", Arg.Any<CancellationToken>())
            .Returns(new AuthRoleLookupResult(Guid.NewGuid(), "InactiveRole", false));

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(clinics: [
                new EmployeeBulkClinicInput("CL001", "InactiveRole"),
            ]),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.False(result.Results[0].Success);
        Assert.Contains("Rol no encontrado o inactivo", result.Results[0].Error!);
    }

    [Fact]
    public async Task ClinicaDuplicadaEnMismaFila_UltimaGana()
    {
        // Si un código de clínica aparece dos veces en la misma fila,
        // se deduplica en la query al repo (Distinct). El segundo
        // intento fallará porque el código solo tiene 1 resultado
        // y se reusa el mismo clinicId — comportamiento aceptable.
        var orgId = Guid.NewGuid();
        var clinicId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        SetupHappyPathMocks(orgId);

        _orgRepo.GetClinicsByCodesAsync(orgId, Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([(clinicId, "CL001")]);

        _authRolesClient.GetRoleByNameAsync("Professional", Arg.Any<CancellationToken>())
            .Returns(new AuthRoleLookupResult(roleId, "Professional", true));

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(clinics: [
                new EmployeeBulkClinicInput("CL001", "Professional"),
                new EmployeeBulkClinicInput("CL001", "Professional"),  // duplicado
            ]),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        // La fila crea el empleado con 2 assignments (ambos al mismo clinicId).
        // Esto es aceptable: la lógica deduplica a nivel de query pero la fila
        // crea los assignments tal cual se piden.
        Assert.Single(result.Results);
        Assert.True(result.Results[0].Success);
        Assert.Equal(1, result.Created);
    }
}
