using CoppAddresd.Application.Features.Professionals;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.Professionals;

public class BulkCreateEmployeesTests
{
    private readonly IEmployeeRepository _employeeRepo = Substitute.For<IEmployeeRepository>();
    private readonly IOrganizationRepository _orgRepo = Substitute.For<IOrganizationRepository>();
    private readonly ILogger<BulkCreateEmployeesCommandHandler> _logger =
        Substitute.For<ILogger<BulkCreateEmployeesCommandHandler>>();

    private BulkCreateEmployeesCommandHandler CreateHandler()
        => new(_employeeRepo, _orgRepo, _logger);

    private static Organization Org(Guid id) =>
        new() { Id = id, Code = "test-org", Name = "Test Org" };

    private static ProfessionalType ProfType(Guid id, string name) =>
        new() { Id = id, Code = "CODE", Name = name, IsActive = true, CreatedAt = DateTime.UtcNow };

    private static BulkEmployeeRowInput Row(
        string firstName = "Ana",
        string lastName = "López",
        string email = "ana@test.com",
        string? professionalTypeName = null,
        string status = "activo") =>
        new(firstName, lastName, email, professionalTypeName, status);

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

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(professionalTypeName: "Médico Cirujano"),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.True(result.Results[0].Success);
        Assert.NotNull(result.Results[0].EmployeeId);
        Assert.Equal(1, result.Created);
        Assert.Equal(0, result.Failed);

        // Verificar que se creó con extensión profesional
        var employee = result.Results[0].EmployeeId!.Value;
        await _employeeRepo.Received(1).AddAsync(
            Arg.Is<Employee>(e => e.Professional != null && e.Professional.ProfessionalTypeId == typeId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FilaValidaNoClinica_CreaEmpleadoSinExtension()
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

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(professionalTypeName: null),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.True(result.Results[0].Success);
        Assert.NotNull(result.Results[0].EmployeeId);
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
        _orgRepo.GetOrganizationByIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(Org(orgId));
        _orgRepo.ListProfessionalTypesAsync(Arg.Any<CancellationToken>())
            .Returns([]);
        _employeeRepo.EmailExistsInOrganizationAsync(orgId, Arg.Any<string>(), null, Arg.Any<CancellationToken>())
            .Returns(false);
        _employeeRepo.AddAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Employee>());

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
        _orgRepo.GetOrganizationByIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(Org(orgId));
        _orgRepo.ListProfessionalTypesAsync(Arg.Any<CancellationToken>())
            .Returns([ProfType(typeId, "médico cirujano")]);
        _employeeRepo.EmailExistsInOrganizationAsync(orgId, Arg.Any<string>(), null, Arg.Any<CancellationToken>())
            .Returns(false);
        _employeeRepo.AddAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Employee>());

        var handler = CreateHandler();
        var command = new BulkCreateEmployeesCommand(orgId, [
            Row(email: "doc@test.com", professionalTypeName: "MÉDICO CIRUJANO"),
        ]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.True(result.Results[0].Success);
        Assert.Equal(1, result.Created);
    }

    [Fact]
    public async Task StatusCaseInsensitive_MapeaCorrectamente()
    {
        var orgId = Guid.NewGuid();
        _orgRepo.GetOrganizationByIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(Org(orgId));
        _orgRepo.ListProfessionalTypesAsync(Arg.Any<CancellationToken>())
            .Returns([]);
        _employeeRepo.EmailExistsInOrganizationAsync(orgId, Arg.Any<string>(), null, Arg.Any<CancellationToken>())
            .Returns(false);

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

    [Fact]
    public async Task ProfessionalTypeNameVacio_EsEmpleadoNoClinico()
    {
        var orgId = Guid.NewGuid();
        _orgRepo.GetOrganizationByIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(Org(orgId));
        _orgRepo.ListProfessionalTypesAsync(Arg.Any<CancellationToken>())
            .Returns([ProfType(Guid.NewGuid(), "Médico")]);
        _employeeRepo.EmailExistsInOrganizationAsync(orgId, Arg.Any<string>(), null, Arg.Any<CancellationToken>())
            .Returns(false);
        _employeeRepo.AddAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Employee>());

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
}
