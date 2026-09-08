using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.Patients;

public class BulkCreatePatientsTests
{
    private readonly IPatientRepository _repository = Substitute.For<IPatientRepository>();
    private readonly ILogger<BulkCreatePatientsCommandHandler> _logger =
        Substitute.For<ILogger<BulkCreatePatientsCommandHandler>>();

    private BulkCreatePatientsCommandHandler CreateHandler()
        => new(_repository, _logger);

    private static BulkPatientRowInput Row(
        string firstName = "Ana",
        string lastName = "López",
        string? documentNumber = null,
        string? email = null,
        string? status = null) =>
        new(firstName, lastName, documentNumber, email, status);

    [Fact]
    public async Task FilaValida_CreaPaciente()
    {
        _repository
            .GetExistingDocumentNumbersAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _repository.AddAsync(Arg.Any<PatientProfile>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<PatientProfile>());

        var handler = CreateHandler();
        var command = new BulkCreatePatientsCommand(
            ClinicId: null,
            Rows: [Row()],
            CreatedBy: null,
            CreatedByProfessionalId: null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.True(result.Results[0].Success);
        Assert.NotNull(result.Results[0].PatientId);
        Assert.Equal(1, result.Created);
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public async Task FirstNameVacio_FilaFallida()
    {
        _repository
            .GetExistingDocumentNumbersAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var handler = CreateHandler();
        var command = new BulkCreatePatientsCommand(
            ClinicId: null,
            Rows: [Row(firstName: "")],
            CreatedBy: null,
            CreatedByProfessionalId: null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.False(result.Results[0].Success);
        Assert.Contains("nombre", result.Results[0].Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LastNameVacio_FilaFallida()
    {
        _repository
            .GetExistingDocumentNumbersAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var handler = CreateHandler();
        var command = new BulkCreatePatientsCommand(
            ClinicId: null,
            Rows: [Row(lastName: "")],
            CreatedBy: null,
            CreatedByProfessionalId: null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.False(result.Results[0].Success);
        Assert.Contains("apellidos", result.Results[0].Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmailFormatoInvalido_FilaFallida()
    {
        _repository
            .GetExistingDocumentNumbersAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var handler = CreateHandler();
        var command = new BulkCreatePatientsCommand(
            ClinicId: null,
            Rows: [Row(email: "no-es-email")],
            CreatedBy: null,
            CreatedByProfessionalId: null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.False(result.Results[0].Success);
        Assert.Contains("formato", result.Results[0].Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmailVacio_EsValidoOpcional()
    {
        _repository
            .GetExistingDocumentNumbersAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _repository.AddAsync(Arg.Any<PatientProfile>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<PatientProfile>());

        var handler = CreateHandler();
        var command = new BulkCreatePatientsCommand(
            ClinicId: null,
            Rows: [Row(email: "")],
            CreatedBy: null,
            CreatedByProfessionalId: null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.True(result.Results[0].Success);
    }

    [Fact]
    public async Task StatusInvalido_MapeaPorDefectoActivo()
    {
        _repository
            .GetExistingDocumentNumbersAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _repository.AddAsync(Arg.Any<PatientProfile>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<PatientProfile>());

        var handler = CreateHandler();
        var command = new BulkCreatePatientsCommand(
            ClinicId: null,
            Rows: [Row(status: "invalido")],
            CreatedBy: null,
            CreatedByProfessionalId: null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.True(result.Results[0].Success);
        await _repository.Received(1).AddAsync(
            Arg.Is<PatientProfile>(p => p.Status == "Activo"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StatusCaseInsensitive_MapeaCorrectamente()
    {
        _repository
            .GetExistingDocumentNumbersAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _repository.AddAsync(Arg.Any<PatientProfile>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<PatientProfile>());

        var handler = CreateHandler();
        var command = new BulkCreatePatientsCommand(
            ClinicId: null,
            Rows: [
                Row(firstName: "A", lastName: "A", email: "a@test.com", status: "ACTIVO"),
                Row(firstName: "B", lastName: "B", email: "b@test.com", status: "inactivo"),
                Row(firstName: "C", lastName: "C", email: "c@test.com", status: "Inactivo"),
            ],
            CreatedBy: null,
            CreatedByProfessionalId: null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(3, result.Created);
        Assert.Equal(0, result.Failed);
        await _repository.Received(1).AddAsync(
            Arg.Is<PatientProfile>(p => p.Status == "Activo"),
            Arg.Any<CancellationToken>());
        await _repository.Received(2).AddAsync(
            Arg.Is<PatientProfile>(p => p.Status == "Inactivo"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StatusNulo_PorDefectoActivo()
    {
        _repository
            .GetExistingDocumentNumbersAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _repository.AddAsync(Arg.Any<PatientProfile>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<PatientProfile>());

        var handler = CreateHandler();
        var command = new BulkCreatePatientsCommand(
            ClinicId: null,
            Rows: [Row(status: null)],
            CreatedBy: null,
            CreatedByProfessionalId: null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.True(result.Results[0].Success);
        await _repository.Received(1).AddAsync(
            Arg.Is<PatientProfile>(p => p.Status == "Activo"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DocumentoDuplicadoEnBatch_FilaFallida()
    {
        _repository
            .GetExistingDocumentNumbersAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _repository.AddAsync(Arg.Any<PatientProfile>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<PatientProfile>());

        var handler = CreateHandler();
        var command = new BulkCreatePatientsCommand(
            ClinicId: null,
            Rows: [
                Row(documentNumber: "12345"),
                Row(documentNumber: "12345"),
            ],
            CreatedBy: null,
            CreatedByProfessionalId: null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(2, result.Results.Count);
        Assert.True(result.Results[0].Success);
        Assert.False(result.Results[1].Success);
        Assert.Contains("duplicado", result.Results[1].Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, result.Created);
        Assert.Equal(1, result.Failed);
    }

    [Fact]
    public async Task DocumentoDuplicadoEnBatch_CaseInsensitive()
    {
        _repository
            .GetExistingDocumentNumbersAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _repository.AddAsync(Arg.Any<PatientProfile>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<PatientProfile>());

        var handler = CreateHandler();
        var command = new BulkCreatePatientsCommand(
            ClinicId: null,
            Rows: [
                Row(documentNumber: "ABC123"),
                Row(documentNumber: "abc123"),
            ],
            CreatedBy: null,
            CreatedByProfessionalId: null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(2, result.Results.Count);
        Assert.True(result.Results[0].Success);
        Assert.False(result.Results[1].Success);
        Assert.Contains("duplicado", result.Results[1].Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DocumentoExistenteEnBD_FilaFallida()
    {
        _repository
            .GetExistingDocumentNumbersAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(["12345"]);

        var handler = CreateHandler();
        var command = new BulkCreatePatientsCommand(
            ClinicId: null,
            Rows: [Row(documentNumber: "12345")],
            CreatedBy: null,
            CreatedByProfessionalId: null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.False(result.Results[0].Success);
        Assert.Contains("ya está registrado", result.Results[0].Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Failed);
    }

    [Fact]
    public async Task EmailDuplicadoEnBatch_NoEsError()
    {
        // Email duplicado NO es error (solo document)
        _repository
            .GetExistingDocumentNumbersAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _repository.AddAsync(Arg.Any<PatientProfile>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<PatientProfile>());

        var handler = CreateHandler();
        var command = new BulkCreatePatientsCommand(
            ClinicId: null,
            Rows: [
                Row(email: "dup@test.com"),
                Row(email: "dup@test.com"),
            ],
            CreatedBy: null,
            CreatedByProfessionalId: null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(2, result.Created);
        Assert.Equal(0, result.Failed);
        Assert.All(result.Results, r => Assert.True(r.Success));
    }

    [Fact]
    public async Task CapExcedido_ValidadorRechaza()
    {
        var rows = Enumerable.Range(1, 501)
            .Select(i => Row(firstName: $"P{i}", lastName: "Test"))
            .ToList();
        var command = new BulkCreatePatientsCommand(
            ClinicId: null,
            Rows: rows,
            CreatedBy: null,
            CreatedByProfessionalId: null);

        var validator = new BulkCreatePatientsValidator();
        var validationResult = await validator.ValidateAsync(command);

        Assert.False(validationResult.IsValid);
        Assert.Contains(validationResult.Errors, e => e.ErrorMessage.Contains("500"));
    }

    [Fact]
    public async Task MezclaParcialSuccess_ContaCorrectos()
    {
        _repository
            .GetExistingDocumentNumbersAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(["docexistente"]);
        _repository.AddAsync(Arg.Any<PatientProfile>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<PatientProfile>());

        var handler = CreateHandler();
        var command = new BulkCreatePatientsCommand(
            ClinicId: null,
            Rows: [
                Row(firstName: "Ana", lastName: "López"),                          // válida
                Row(firstName: "", lastName: "Test"),                              // nombre vacío
                Row(firstName: "Luis", lastName: "Pérez", email: "bad-email"),    // email inválido
                Row(firstName: "María", lastName: "Gómez", documentNumber: "docexistente"), // dup BD
            ],
            CreatedBy: null,
            CreatedByProfessionalId: null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(4, result.Results.Count);
        Assert.True(result.Results[0].Success);   // Ana
        Assert.False(result.Results[1].Success);  // nombre vacío
        Assert.False(result.Results[2].Success);  // email inválido
        Assert.False(result.Results[3].Success);  // dup BD
        Assert.Equal(1, result.Created);
        Assert.Equal(3, result.Failed);
    }

    [Fact]
    public void Validator_RechazaRowsVacios()
    {
        var command = new BulkCreatePatientsCommand(
            ClinicId: null,
            Rows: [],
            CreatedBy: null,
            CreatedByProfessionalId: null);
        var validator = new BulkCreatePatientsValidator();
        var result = validator.Validate(command);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ClinicId_PasadoAlPaciente()
    {
        var clinicId = Guid.NewGuid();
        _repository
            .GetExistingDocumentNumbersAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _repository.AddAsync(Arg.Any<PatientProfile>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<PatientProfile>());

        var handler = CreateHandler();
        var command = new BulkCreatePatientsCommand(
            ClinicId: clinicId,
            Rows: [Row()],
            CreatedBy: null,
            CreatedByProfessionalId: null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.Results[0].Success);
        await _repository.Received(1).AddAsync(
            Arg.Is<PatientProfile>(p => p.ClinicId == clinicId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClinicaNull_PacienteSinClinica()
    {
        _repository
            .GetExistingDocumentNumbersAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _repository.AddAsync(Arg.Any<PatientProfile>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<PatientProfile>());

        var handler = CreateHandler();
        var command = new BulkCreatePatientsCommand(
            ClinicId: null,
            Rows: [Row()],
            CreatedBy: null,
            CreatedByProfessionalId: null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.Results[0].Success);
        await _repository.Received(1).AddAsync(
            Arg.Is<PatientProfile>(p => p.ClinicId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatorAutoAsignado_CuandoEsProfesional()
    {
        var professionalId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _repository
            .GetExistingDocumentNumbersAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _repository.AddAsync(Arg.Any<PatientProfile>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<PatientProfile>());

        var handler = CreateHandler();
        var command = new BulkCreatePatientsCommand(
            ClinicId: null,
            Rows: [Row()],
            CreatedBy: userId,
            CreatedByProfessionalId: professionalId);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.Results[0].Success);
        await _repository.Received(1).AssignProfessionalAsync(
            Arg.Any<Guid>(),
            professionalId,
            Arg.Any<Guid?>(),
            "Assigned",
            userId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SinProfesional_NoSeAutoAsigna()
    {
        _repository
            .GetExistingDocumentNumbersAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _repository.AddAsync(Arg.Any<PatientProfile>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<PatientProfile>());

        var handler = CreateHandler();
        var command = new BulkCreatePatientsCommand(
            ClinicId: null,
            Rows: [Row()],
            CreatedBy: null,
            CreatedByProfessionalId: null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.Results[0].Success);
        await _repository.DidNotReceive().AssignProfessionalAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<Guid?>(),
            Arg.Any<string>(),
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FirstNameExcesivamenteLargo_FilaFallida()
    {
        _repository
            .GetExistingDocumentNumbersAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var handler = CreateHandler();
        var command = new BulkCreatePatientsCommand(
            ClinicId: null,
            Rows: [Row(firstName: new string('A', 101))],
            CreatedBy: null,
            CreatedByProfessionalId: null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Results);
        Assert.False(result.Results[0].Success);
        Assert.Contains("100 caracteres", result.Results[0].Error!, StringComparison.OrdinalIgnoreCase);
    }
}
