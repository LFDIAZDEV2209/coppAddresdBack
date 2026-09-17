using CoppAddresd.Application.Features.Catalogs;
using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.Patients;

/// <summary>
/// Pruebas de la Fase 4 (patient scoping): frontera de datos por clínica,
/// soft delete y auditoría de actor (created_by/updated_by) en los handlers.
/// </summary>
public class PatientScopingTests
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

    [Fact]
    public async Task Create_ConClinicaActiva_AsignaClinicaYActores()
    {
        var clinicId = Guid.NewGuid();
        var userId = Guid.NewGuid();

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
        _repository
            .GetByMedicalRecordNumberAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((PatientProfile?)null);

        PatientProfile? persisted = null;
        _repository
            .AddAsync(Arg.Do<PatientProfile>(p => persisted = p), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<PatientProfile>());

        _repository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult<PatientProfile?>(persisted));

        var handler = new CreatePatientCommandHandler(_repository, _catalogs, _createLogger);

        var result = await handler.Handle(
            new CreatePatientCommand(
                MedicalRecordNumber: null,
                FirstName: "María",
                MiddleName: null,
                LastName: "Gómez",
                DocumentTypeId: null,
                DocumentNumber: null,
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
                Status: null,
                Notes: null,
                ClinicId: clinicId,
                LocationId: null,
                CreatedBy: userId,
                CreatedByProfessionalId: null,
                Diagnoses: null,
                Medications: null,
                Allergies: null,
                VitalSigns: null
            ),
            CancellationToken.None
        );

        Assert.Equal(clinicId, result.ClinicId);
        Assert.Equal(clinicId, persisted!.ClinicId);
        Assert.Equal(userId, persisted.CreatedBy);
        Assert.Equal(userId, persisted.UpdatedBy);
    }

    [Fact]
    public async Task Update_RegistraUpdatedBy()
    {
        var userId = Guid.NewGuid();
        var existing = new PatientProfile
        {
            Id = Guid.NewGuid(),
            MedicalRecordNumber = "MRN-0001",
            FirstName = "Ana",
            LastName = "Pérez",
            Status = "Activo",
            ClinicId = Guid.NewGuid(),
        };

        _repository.GetByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);
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
        _repository
            .GetByIdAsync(existing.Id, Arg.Any<CancellationToken>())
            .Returns(callInfo => existing);

        var handler = new UpdatePatientCommandHandler(_repository, _catalogs, _updateLogger);

        var result = await handler.Handle(
            new UpdatePatientCommand(
                Id: existing.Id,
                MedicalRecordNumber: existing.MedicalRecordNumber,
                FirstName: "Ana María",
                MiddleName: null,
                LastName: existing.LastName,
                DocumentTypeId: null,
                DocumentNumber: null,
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
                Status: null,
                Notes: null,
                UpdatedBy: userId,
                Diagnoses: null,
                Medications: null,
                Allergies: null,
                VitalSigns: null
            ),
            CancellationToken.None
        );

        Assert.NotNull(result);
        Assert.Equal(userId, existing.UpdatedBy);
        Assert.Equal("Ana María", existing.FirstName);
        await _repository.Received(1).UpdateAsync(existing, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_UsaSoftDeleteYRegistraActor()
    {
        var deletedBy = Guid.NewGuid();
        var existing = new PatientProfile
        {
            Id = Guid.NewGuid(),
            MedicalRecordNumber = "MRN-0002",
            FirstName = "Luis",
            LastName = "Torres",
            Status = "Activo",
        };

        _repository.GetByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);

        var handler = new DeletePatientCommandHandler(_repository);

        var result = await handler.Handle(
            new DeletePatientCommand(existing.Id, deletedBy),
            CancellationToken.None
        );

        Assert.True(result);
        await _repository.Received(1).SoftDeleteAsync(existing, Arg.Any<CancellationToken>());
        Assert.Equal(deletedBy, existing.UpdatedBy);
    }

    [Fact]
    public async Task List_PasaClinicIdAlRepositorio()
    {
        var clinicId = Guid.NewGuid();
        var patient = new PatientProfile
        {
            Id = Guid.NewGuid(),
            MedicalRecordNumber = "MRN-0003",
            FirstName = "Carmen",
            LastName = "Ríos",
            Status = "Activo",
            ClinicId = clinicId,
        };

        _repository
            .ListAsync(
                1,
                20,
                null,
                null,
                null,
                clinicId,
                null,
                null,
                "desc",
                null,
                Arg.Any<CancellationToken>()
            )
            .Returns((new[] { patient }, 1));

        var handler = new ListPatientsQueryHandler(_repository);

        var result = await handler.Handle(
            new ListPatientsQuery(ClinicId: clinicId),
            CancellationToken.None
        );

        Assert.Equal(1, result.Total);
        Assert.Single(result.Data);
        Assert.Equal(clinicId, result.Data[0].ClinicId);
    }

    [Fact]
    public async Task List_SinClinica_NoFiltraPorClinica()
    {
        _repository
            .ListAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((new List<PatientProfile>(), 0));

        var handler = new ListPatientsQueryHandler(_repository);

        await handler.Handle(new ListPatientsQuery(), CancellationToken.None);

        await _repository
            .Received(1)
            .ListAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<Guid?>(),
                Arg.Is<Guid?>(v => v == null),
                Arg.Is<Guid?>(v => v == null),
                Arg.Is<string?>(v => v == null),
                Arg.Is<string?>(v => v == "desc"),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task List_ConSortByValido_PasaOrdenAlRepositorio()
    {
        _repository
            .ListAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((new List<PatientProfile>(), 0));

        var handler = new ListPatientsQueryHandler(_repository);

        await handler.Handle(
            new ListPatientsQuery(SortBy: "firstName", SortDir: "asc"),
            CancellationToken.None
        );

        await _repository
            .Received(1)
            .ListAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Is<string?>(v => v == "firstName"),
                Arg.Is<string?>(v => v == "asc"),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task List_ConSortByFueraDeWhitelist_UsaOrdenPorDefecto()
    {
        _repository
            .ListAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((new List<PatientProfile>(), 0));

        var handler = new ListPatientsQueryHandler(_repository);

        // sortBy no permitido → null (CreatedAt desc); sortDir inválido → desc.
        await handler.Handle(
            new ListPatientsQuery(SortBy: "password; DROP TABLE", SortDir: "mal"),
            CancellationToken.None
        );

        await _repository
            .Received(1)
            .ListAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Is<string?>(v => v == null),
                Arg.Is<string?>(v => v == "desc"),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task List_ConEstadoNormalizaMayusculasYPasaAlRepositorio()
    {
        _repository
            .ListAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((new List<PatientProfile>(), 0));

        var handler = new ListPatientsQueryHandler(_repository);

        await handler.Handle(new ListPatientsQuery(StateCode: "ca"), CancellationToken.None);

        await _repository
            .Received(1)
            .ListAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Is<string?>(v => v == "CA"),
                Arg.Any<CancellationToken>()
            );
    }
}
