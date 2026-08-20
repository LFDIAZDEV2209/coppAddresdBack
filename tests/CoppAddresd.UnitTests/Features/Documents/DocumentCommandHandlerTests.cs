using CoppAddresd.Application.Features.Documents;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.Documents;

public class DocumentCommandHandlerTests
{
    private readonly IDocumentRepository _repository = Substitute.For<IDocumentRepository>();

    private CreateDocumentCommandHandler CreateHandler() => new(_repository);

    private static ClinicalDocumentType TipoPdf() => new()
    {
        Id = Guid.NewGuid(),
        CategoryId = Guid.NewGuid(),
        Code = "LAB_CBC",
        Name = "Hemograma completo",
        AllowedExtensions = ["pdf"],
        IsActive = true,
    };

    // ---------------------------------------------------------------- intent

    [Fact]
    public async Task Intent_PacienteInexistente_LanzaNotFound()
    {
        var patientId = Guid.NewGuid();
        _repository.PatientExistsAsync(patientId, Arg.Any<CancellationToken>()).Returns(false);

        var handler = new CreateDocumentUploadIntentCommandHandler(_repository);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new CreateDocumentUploadIntentCommand(patientId, Guid.NewGuid(), "cbc.pdf", null),
                CancellationToken.None));
    }

    [Fact]
    public async Task Intent_TipoInactivo_LanzaUnprocessableEntity()
    {
        var patientId = Guid.NewGuid();
        _repository.PatientExistsAsync(patientId, Arg.Any<CancellationToken>()).Returns(true);
        _repository.GetTypeByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new ClinicalDocumentType { Id = Guid.NewGuid(), IsActive = false });

        var handler = new CreateDocumentUploadIntentCommandHandler(_repository);

        await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            handler.Handle(new CreateDocumentUploadIntentCommand(patientId, Guid.NewGuid(), "cbc.pdf", null),
                CancellationToken.None));
    }

    [Fact]
    public async Task Intent_ExtensionNoPermitida_LanzaBusinessRuleViolation()
    {
        var patientId = Guid.NewGuid();
        _repository.PatientExistsAsync(patientId, Arg.Any<CancellationToken>()).Returns(true);
        _repository.GetTypeByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(TipoPdf());

        var handler = new CreateDocumentUploadIntentCommandHandler(_repository);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            handler.Handle(new CreateDocumentUploadIntentCommand(patientId, Guid.NewGuid(), "cbc.xlsx", null),
                CancellationToken.None));
    }

    [Fact]
    public async Task Intent_ParentInexistente_LanzaNotFound()
    {
        var patientId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        _repository.PatientExistsAsync(patientId, Arg.Any<CancellationToken>()).Returns(true);
        _repository.GetTypeByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(TipoPdf());
        _repository.GetRootAsync(parentId, Arg.Any<CancellationToken>()).Returns((Document?)null);

        var handler = new CreateDocumentUploadIntentCommandHandler(_repository);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new CreateDocumentUploadIntentCommand(patientId, Guid.NewGuid(), "cbc.pdf", parentId),
                CancellationToken.None));
    }

    [Fact]
    public async Task Intent_Valido_GeneraClaveBajoDocuments()
    {
        var patientId = Guid.NewGuid();
        _repository.PatientExistsAsync(patientId, Arg.Any<CancellationToken>()).Returns(true);
        _repository.GetTypeByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(TipoPdf());

        var handler = new CreateDocumentUploadIntentCommandHandler(_repository);

        var result = await handler.Handle(
            new CreateDocumentUploadIntentCommand(patientId, Guid.NewGuid(), "CBC.pdf", null),
            CancellationToken.None);

        Assert.StartsWith($"documents/{patientId}/", result.StorageKey);
        Assert.EndsWith(".pdf", result.StorageKey);
        Assert.Equal(900, result.ExpiresInSeconds);
    }

    // ---------------------------------------------------------------- create

    [Fact]
    public async Task Create_TipoInactivo_LanzaUnprocessableEntity()
    {
        _repository.GetTypeByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new ClinicalDocumentType { Id = Guid.NewGuid(), IsActive = false });

        var handler = CreateHandler();

        await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            handler.Handle(ComandoBase(), CancellationToken.None));
    }

    [Fact]
    public async Task Create_ClaveFueraDeDocuments_LanzaBusinessRuleViolation()
    {
        _repository.GetTypeByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(TipoPdf());
        _repository.PatientExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);

        var handler = CreateHandler();

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            handler.Handle(ComandoBase() with { StorageKey = "media/foo/x.pdf" }, CancellationToken.None));
    }

    [Fact]
    public async Task Create_PacienteInexistente_LanzaNotFound()
    {
        var patientId = Guid.NewGuid();
        _repository.GetTypeByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(TipoPdf());
        _repository.PatientExistsAsync(patientId, Arg.Any<CancellationToken>()).Returns(false);

        var handler = CreateHandler();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(ComandoBase() with { PatientId = patientId }, CancellationToken.None));
    }

    [Fact]
    public async Task Create_Valido_HeredaClinicaDelPaciente()
    {
        var patientId = Guid.NewGuid();
        var clinicId = Guid.NewGuid();
        _repository.GetTypeByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(TipoPdf());
        _repository.PatientExistsAsync(patientId, Arg.Any<CancellationToken>()).Returns(true);
        _repository.GetPatientClinicIdAsync(patientId, Arg.Any<CancellationToken>()).Returns(clinicId);

        Document? persisted = null;
        _repository.AddAsync(Arg.Do<Document>(d => persisted = d), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var savedId = Guid.NewGuid();
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => Persistido(persisted!, savedId, TipoPdf()));

        var handler = CreateHandler();

        var result = await handler.Handle(ComandoBase() with { PatientId = patientId }, CancellationToken.None);

        Assert.Equal(clinicId, persisted!.ClinicId);
        Assert.Equal(1, persisted.Version);
        Assert.Equal(patientId, persisted.PatientId);
        Assert.Equal("Ready", persisted.Status);
        Assert.Equal(result.Id, savedId);
    }

    [Fact]
    public async Task Create_ConParent_AsignaVersionSiguienteYHeredaClinic()
    {
        var parentId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var clinicId = Guid.NewGuid();

        _repository.GetTypeByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(TipoPdf());
        _repository.GetRootAsync(parentId, Arg.Any<CancellationToken>())
            .Returns(new Document { Id = parentId, PatientId = patientId, ClinicId = clinicId, Version = 1 });
        _repository.GetNextVersionAsync(parentId, Arg.Any<CancellationToken>()).Returns(2);

        Document? persisted = null;
        _repository.AddAsync(Arg.Do<Document>(d => persisted = d), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var savedId = Guid.NewGuid();
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => Persistido(persisted!, savedId, TipoPdf()));

        var handler = CreateHandler();

        var result = await handler.Handle(ComandoBase() with
        {
            PatientId = null,
            ParentDocumentId = parentId,
        }, CancellationToken.None);

        Assert.Equal(2, persisted!.Version);
        Assert.Equal(patientId, persisted.PatientId);
        Assert.Equal(clinicId, persisted.ClinicId);
        Assert.Equal(parentId, persisted.ParentDocumentId);
        Assert.Equal(result.Id, savedId);
    }

    // ---------------------------------------------------------------- update

    [Fact]
    public async Task Update_DocumentoInexistente_DevuelveNull()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Document?)null);

        var handler = new UpdateDocumentCommandHandler(_repository);

        var result = await handler.Handle(new UpdateDocumentCommand(Guid.NewGuid(), null, null, null, null, null),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Update_EstadoInvalido_LanzaUnprocessableEntity()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new Document { Id = Guid.NewGuid(), StorageKey = "documents/x.pdf" });

        var handler = new UpdateDocumentCommandHandler(_repository);

        await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            handler.Handle(new UpdateDocumentCommand(Guid.NewGuid(), null, null, null, "Borrado", null),
                CancellationToken.None));
    }

    [Fact]
    public async Task Update_Valido_ActualizaMetadata()
    {
        var docId = Guid.NewGuid();
        var type = TipoPdf();
        type.Category = new DocumentCategory { Id = type.CategoryId, Code = "LABORATORIO", Name = "Laboratorio" };
        var doc = new Document
        {
            Id = docId,
            Title = "Viejo",
            StorageKey = "documents/x.pdf",
            Status = "Ready",
            DocumentType = type,
            DocumentTypeId = type.Id,
        };
        _repository.GetByIdAsync(docId, Arg.Any<CancellationToken>()).Returns(doc);

        var handler = new UpdateDocumentCommandHandler(_repository);

        var result = await handler.Handle(
            new UpdateDocumentCommand(docId, "Nuevo título", "desc", null, "Archived", Guid.NewGuid()),
            CancellationToken.None);

        Assert.Equal("Nuevo título", doc.Title);
        Assert.Equal("Archived", doc.Status);
        Assert.Equal("desc", doc.Description);
        Assert.NotNull(doc.UpdatedAt);
        Assert.Equal("Nuevo título", result!.Title);
    }

    // ---------------------------------------------------------------- delete

    [Fact]
    public async Task Delete_Inexistente_DevuelveFalse()
    {
        _repository.SoftDeleteFamilyAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new DeleteDocumentCommandHandler(_repository);

        var result = await handler.Handle(new DeleteDocumentCommand(Guid.NewGuid(), null), CancellationToken.None);

        Assert.False(result);
    }

    private static Document Persistido(Document source, Guid id, ClinicalDocumentType type)
        => new()
        {
            Id = id,
            PatientId = source.PatientId,
            ClinicId = source.ClinicId,
            DocumentTypeId = type.Id,
            Title = source.Title,
            StorageKey = source.StorageKey,
            Version = source.Version,
            ParentDocumentId = source.ParentDocumentId,
            Status = source.Status,
            CreatedAt = DateTime.UtcNow,
            DocumentType = type,
        };

    private static CreateDocumentCommand ComandoBase() => new(
        PatientId: Guid.NewGuid(),
        ProfessionalId: null,
        DocumentTypeId: Guid.NewGuid(),
        Title: "Hemograma",
        Description: null,
        StorageKey: "documents/x/cbc.pdf",
        ContentType: "application/pdf",
        FileSizeBytes: 1024,
        ParentDocumentId: null,
        ClinicId: null,
        UploadedBy: Guid.NewGuid(),
        CreatedBy: Guid.NewGuid());
}