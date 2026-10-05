using CoppAddresd.Application.Features.Catalogs;
using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.Patients;

/// <summary>
/// Unicidad global del número de documento (case-insensitive): el alta y la
/// edición rechazan con BusinessRuleViolation (409) cuando otro paciente ya
/// tiene el documento; el propio paciente y los documentos vacíos no se
/// verifican.
/// </summary>
public class PatientDocumentUniquenessTests
{
    private readonly IPatientRepository _repository = Substitute.For<IPatientRepository>();
    private readonly ICatalogRepository _catalogs = Substitute.For<ICatalogRepository>();
    private readonly ILogger<CreatePatientCommandHandler> _createLogger = Substitute.For<
        ILogger<CreatePatientCommandHandler>
    >();
    private readonly ILogger<UpdatePatientCommandHandler> _updateLogger = Substitute.For<
        ILogger<UpdatePatientCommandHandler>
    >();

    private static readonly CatalogValidationResult OkCatalogs = new(
        DocumentTypeExists: true,
        EthnicityExists: true,
        BloodTypeExists: true,
        CountryExists: true,
        StateExists: true,
        StateInCountry: true,
        CityExists: true,
        CityInState: true,
        ExistingInsurerIds: new HashSet<Guid>(),
        ExistingIcd10CodeIds: new HashSet<Guid>(),
        ExistingMedicationIds: new HashSet<Guid>(),
        ExistingAllergenIds: new HashSet<Guid>()
    );

    private static CreatePatientCommand CreateCommand(string? documentNumber) =>
        new(
            MedicalRecordNumber: null,
            FirstName: "Mauricio",
            MiddleName: null,
            LastName: "Polo",
            DocumentTypeId: null,
            DocumentNumber: documentNumber,
            DateOfBirth: null,
            Gender: null,
            EthnicityId: null,
            BloodTypeId: null,
            PhoneCountryCode: null,
            PhoneNumber: null,
            Email: null,
            Address: null,
            CityId: null,
            StateId: null,
            CountryId: null,
            PostalCode: null,
            EmergencyContact: null,
            InsurerId: null,
            MemberId: null,
            MaritalStatus: null,
            SmokingStatus: null,
            AlcoholStatus: null,
            ExerciseLevel: null,
            Disability: null,
            HospitalizationHistory: null,
            SurgeryHistory: null,
            Status: "Pendiente",
            Notes: null,
            ClinicId: null,
            LocationId: null,
            CreatedBy: null,
            CreatedByProfessionalId: null,
            Diagnoses: null,
            Medications: null,
            Allergies: null,
            VitalSigns: null
        );

    private static UpdatePatientCommand UpdateCommand(Guid id, string? documentNumber) =>
        new(
            Id: id,
            MedicalRecordNumber: null,
            FirstName: "Ana",
            MiddleName: null,
            LastName: "Pérez",
            DocumentTypeId: null,
            DocumentNumber: documentNumber,
            DateOfBirth: null,
            Gender: null,
            EthnicityId: null,
            BloodTypeId: null,
            PhoneCountryCode: null,
            PhoneNumber: null,
            Email: null,
            Address: null,
            CityId: null,
            StateId: null,
            CountryId: null,
            PostalCode: null,
            EmergencyContact: null,
            InsurerId: null,
            MemberId: null,
            MaritalStatus: null,
            SmokingStatus: null,
            AlcoholStatus: null,
            ExerciseLevel: null,
            Disability: null,
            HospitalizationHistory: null,
            SurgeryHistory: null,
            Status: "Activo",
            Notes: null,
            UpdatedBy: null,
            Diagnoses: null,
            Medications: null,
            Allergies: null,
            VitalSigns: null
        );

    private static PatientProfile ExistingPatient(string? documentNumber) =>
        new()
        {
            Id = Guid.NewGuid(),
            FirstName = "Ana",
            LastName = "Pérez",
            DocumentNumber = documentNumber,
            Status = "Activo",
        };

    private void StubOkCatalogs() =>
        _catalogs
            .ValidateAsync(
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(OkCatalogs);

    [Fact]
    public async Task Create_DocumentoDuplicado_RechazaConBusinessRuleViolation()
    {
        StubOkCatalogs();
        _repository
            .GetExistingDocumentNumbersAsync(
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new List<string> { "12345678" });
        var handler = new CreatePatientCommandHandler(_repository, _catalogs, _createLogger);

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            handler.Handle(CreateCommand("12345678"), CancellationToken.None)
        );

        Assert.Contains("12345678", exception.Message);
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Create_DocumentoNuevo_CreaPaciente()
    {
        StubOkCatalogs();
        _repository
            .GetExistingDocumentNumbersAsync(
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new List<string>());
        PatientProfile? persisted = null;
        _repository
            .AddAsync(Arg.Do<PatientProfile>(p => persisted = p), Arg.Any<CancellationToken>())
            .Returns(_ => persisted!);
        _repository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => persisted);
        var handler = new CreatePatientCommandHandler(_repository, _catalogs, _createLogger);

        var result = await handler.Handle(CreateCommand("87654321"), CancellationToken.None);

        Assert.NotNull(result);
        await _repository.Received(1).AddAsync(Arg.Any<PatientProfile>(), Arg.Any<CancellationToken>());
        Assert.Equal("87654321", persisted!.DocumentNumber);
    }

    [Fact]
    public async Task Update_DocumentoCambiadoADuplicado_RechazaConBusinessRuleViolation()
    {
        StubOkCatalogs();
        var entity = ExistingPatient("11111111");
        _repository
            .GetByIdAsync(entity.Id, Arg.Any<CancellationToken>())
            .Returns(entity);
        _repository
            .GetExistingDocumentNumbersAsync(
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new List<string> { "22222222" });
        var handler = new UpdatePatientCommandHandler(_repository, _catalogs, _updateLogger);

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            handler.Handle(UpdateCommand(entity.Id, "22222222"), CancellationToken.None)
        );

        Assert.Contains("22222222", exception.Message);
        await _repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task Update_MismoDocumento_NoVerificaDuplicados()
    {
        StubOkCatalogs();
        var entity = ExistingPatient("11111111");
        _repository
            .GetByIdAsync(entity.Id, Arg.Any<CancellationToken>())
            .Returns(entity);
        var handler = new UpdatePatientCommandHandler(_repository, _catalogs, _updateLogger);

        var result = await handler.Handle(
            UpdateCommand(entity.Id, " 11111111 "),
            CancellationToken.None
        );

        Assert.NotNull(result);
        await _repository
            .DidNotReceive()
            .GetExistingDocumentNumbersAsync(
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>()
            );
    }
}
