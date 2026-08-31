using CoppAddresd.Application.Features.Catalogs;
using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.Patients;

/// <summary>
/// Pruebas del alcance de datos "propios" (Patients.ViewOwn): auto-asignación
/// al crear un paciente por un profesional clínico, filtro "mis pacientes" en
/// el listado y validación de la asignación en los comandos.
/// </summary>
public class PatientOwnScopeTests
{
    private readonly IPatientRepository _repository = Substitute.For<IPatientRepository>();
    private readonly ICatalogRepository _catalogs = Substitute.For<ICatalogRepository>();
    private readonly IEmployeeRepository _employees = Substitute.For<IEmployeeRepository>();
    private readonly ILogger<CreatePatientCommandHandler> _logger = Substitute.For<
        ILogger<CreatePatientCommandHandler>
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

    private static CreatePatientCommand BuildCreateCommand(Guid? createdByProfessionalId) =>
        new(
            MedicalRecordNumber: null,
            FirstName: "Pedro",
            MiddleName: null,
            LastName: "Luna",
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
            ClinicId: null,
            LocationId: null,
            CreatedBy: Guid.NewGuid(),
            CreatedByProfessionalId: createdByProfessionalId,
            Diagnoses: null,
            Medications: null,
            Allergies: null,
            VitalSigns: null
        );

    [Fact]
    public async Task Create_CreadorEsProfesional_AutoAsignaAlPaciente()
    {
        var professionalId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();

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
            .Returns(callInfo => Task.FromResult(persisted!));

        var handler = new CreatePatientCommandHandler(_repository, _catalogs, _logger);

        var command = BuildCreateCommand(professionalId);
        await handler.Handle(command, CancellationToken.None);

        // El paciente creado queda asignado al profesional del creador.
        await _repository
            .Received(1)
            .AssignProfessionalAsync(
                persisted!.Id,
                professionalId,
                Arg.Any<Guid?>(),
                "Assigned",
                command.CreatedBy,
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Create_CreadorNoEsProfesional_NoAutoAsigna()
    {
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
            .Returns(callInfo => Task.FromResult(persisted!));

        var handler = new CreatePatientCommandHandler(_repository, _catalogs, _logger);

        await handler.Handle(
            BuildCreateCommand(createdByProfessionalId: null),
            CancellationToken.None
        );

        await _repository
            .DidNotReceiveWithAnyArgs()
            .AssignProfessionalAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<Guid?>(),
                Arg.Any<string>(),
                Arg.Any<Guid?>()
            );
    }

    [Fact]
    public async Task List_ConAlcancePropio_PasaProfessionalIdAlRepositorio()
    {
        var professionalId = Guid.NewGuid();
        var patient = new PatientProfile
        {
            Id = Guid.NewGuid(),
            MedicalRecordNumber = "MRN-0004",
            FirstName = "Sofía",
            LastName = "Vega",
            Status = "Activo",
        };

        _repository
            .ListAsync(
                1,
                20,
                null,
                null,
                null,
                null,
                professionalId,
                null,
                "desc",
                Arg.Any<CancellationToken>()
            )
            .Returns((new[] { patient }, 1));

        var handler = new ListPatientsQueryHandler(_repository);

        var result = await handler.Handle(
            new ListPatientsQuery(OwnProfessionalId: professionalId),
            CancellationToken.None
        );

        Assert.Equal(1, result.Total);
        await _repository
            .Received(1)
            .ListAsync(
                1,
                20,
                null,
                null,
                null,
                null,
                professionalId,
                null,
                "desc",
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Assign_ProfesionalInexistente_LanzaNotFound()
    {
        var patientId = Guid.NewGuid();
        var professionalId = Guid.NewGuid();

        _repository.ExistsAsync(patientId, Arg.Any<CancellationToken>()).Returns(true);
        _employees
            .GetByProfessionalIdAsync(professionalId, Arg.Any<CancellationToken>())
            .Returns((Employee?)null);

        var handler = new AssignPatientProfessionalCommandHandler(_repository, _employees);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(
                new AssignPatientProfessionalCommand(patientId, professionalId, null, null, null),
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task Assign_FlujoFeliz_DevuelveAsignacion()
    {
        var patientId = Guid.NewGuid();
        var professionalId = Guid.NewGuid();
        var clinicId = Guid.NewGuid();
        var view = new PatientProfessionalAssignmentView(
            professionalId,
            "Dr. Ana Ríos",
            "Médico",
            "Assigned",
            "Active",
            DateTime.UtcNow
        );

        _repository.ExistsAsync(patientId, Arg.Any<CancellationToken>()).Returns(true);
        _employees
            .GetByProfessionalIdAsync(professionalId, Arg.Any<CancellationToken>())
            .Returns(new Employee { Professional = new Professional { Id = professionalId } });
        _repository
            .ListAssignmentsAsync(patientId, Arg.Any<CancellationToken>())
            .Returns(new[] { view });

        var handler = new AssignPatientProfessionalCommandHandler(_repository, _employees);

        var result = await handler.Handle(
            new AssignPatientProfessionalCommand(
                patientId,
                professionalId,
                clinicId,
                "Assigned",
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        Assert.Equal(professionalId, result.ProfessionalId);
        await _repository
            .Received(1)
            .AssignProfessionalAsync(
                patientId,
                professionalId,
                clinicId,
                "Assigned",
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task IsAssigned_DelegaAlRepositorio()
    {
        var patientId = Guid.NewGuid();
        var professionalId = Guid.NewGuid();

        _repository
            .IsAssignedToProfessionalAsync(patientId, professionalId, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new PatientIsAssignedQueryHandler(_repository);

        var result = await handler.Handle(
            new PatientIsAssignedQuery(patientId, professionalId),
            CancellationToken.None
        );

        Assert.True(result);
    }
}
