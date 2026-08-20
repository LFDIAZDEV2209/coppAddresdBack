using CoppAddresd.Application.Features.Professionals;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.Professionals;

public class EmployeeCommandHandlerTests
{
    private readonly IEmployeeRepository _employeeRepository = Substitute.For<IEmployeeRepository>();
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly ILogger<CreateEmployeeCommandHandler> _createLogger =
        Substitute.For<ILogger<CreateEmployeeCommandHandler>>();

    private CreateEmployeeCommandHandler CreateHandler()
        => new(_employeeRepository, _organizationRepository, _createLogger);

    private static CreateEmployeeCommand ComandoBase(Guid organizationId) => new(
        organizationId,
        "Ana",
        null,
        "López",
        "ana@mediquer.com",
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null);

    [Fact]
    public async Task Handle_OrganizacionInexistente_LanzaUnprocessableEntity()
    {
        var organizationId = Guid.NewGuid();
        _organizationRepository.GetOrganizationByIdAsync(organizationId, Arg.Any<CancellationToken>())
            .Returns((Organization?)null);

        var handler = CreateHandler();

        await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            handler.Handle(ComandoBase(organizationId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_EmailDuplicado_LanzaBusinessRuleViolation()
    {
        var organizationId = Guid.NewGuid();
        _organizationRepository.GetOrganizationByIdAsync(organizationId, Arg.Any<CancellationToken>())
            .Returns(new Organization { Id = organizationId, Code = "mediquer", Name = "MediQuer" });
        _employeeRepository.EmailExistsInOrganizationAsync(
                organizationId, "ana@mediquer.com", null, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = CreateHandler();

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            handler.Handle(ComandoBase(organizationId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_EmailConMayusculas_NormalizaYValidaEnMinusculas()
    {
        var organizationId = Guid.NewGuid();
        _organizationRepository.GetOrganizationByIdAsync(organizationId, Arg.Any<CancellationToken>())
            .Returns(new Organization { Id = organizationId, Code = "mediquer", Name = "MediQuer" });
        _employeeRepository.EmailExistsInOrganizationAsync(
                organizationId, "ana@mediquer.com", null, Arg.Any<CancellationToken>())
            .Returns(false);

        Employee? persisted = null;
        _employeeRepository.AddAsync(Arg.Do<Employee>(e => persisted = e), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Employee>());

        _employeeRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var employee = persisted ?? throw new InvalidOperationException("Empleado no persistido.");
                employee.Organization = new Organization { Id = organizationId, Code = "mediquer", Name = "MediQuer" };
                return employee;
            });

        var handler = CreateHandler();

        var result = await handler.Handle(
            ComandoBase(organizationId) with { Email = "ANA@MEDIQUER.COM" },
            CancellationToken.None);

        Assert.Equal("ana@mediquer.com", result.Email);
        Assert.Equal("Invited", result.Status);
    }

    [Fact]
    public async Task Handle_ConExtensionProfesional_PersisteEmpleadoConProfessional()
    {
        var organizationId = Guid.NewGuid();
        var professionalTypeId = Guid.NewGuid();
        var clinicId = Guid.NewGuid();
        var specialtyId = Guid.NewGuid();

        _organizationRepository.GetOrganizationByIdAsync(organizationId, Arg.Any<CancellationToken>())
            .Returns(new Organization { Id = organizationId, Code = "mediquer", Name = "MediQuer" });
        _organizationRepository.GetProfessionalTypeByIdAsync(professionalTypeId, Arg.Any<CancellationToken>())
            .Returns(new ProfessionalType { Id = professionalTypeId, Code = "PHYSICIAN", Name = "Physician (MD/DO)" });
        _employeeRepository.EmailExistsInOrganizationAsync(
                organizationId, "ana@mediquer.com", null, Arg.Any<CancellationToken>())
            .Returns(false);
        _employeeRepository.SpecialtyExistsAsync(specialtyId, Arg.Any<CancellationToken>()).Returns(true);
        _employeeRepository.ClinicExistsAsync(clinicId, Arg.Any<CancellationToken>()).Returns(true);

        Employee? persisted = null;
        _employeeRepository.AddAsync(Arg.Do<Employee>(e => persisted = e), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Employee>());

        _employeeRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => BuildDetalle(persisted!, organizationId, "MediQuer", "Physician (MD/DO)"));

        var handler = CreateHandler();

        var command = ComandoBase(organizationId) with
        {
            ProfessionalTypeId = professionalTypeId,
            Clinics = [new ClinicAssignmentInput(clinicId, true, "Active")],
            SpecialtyIds = [specialtyId],
            Licenses = [new LicenseInput("StateLicense", null, "TX-1234", null, "Texas Medical Board", null, null, "Pending")],
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(persisted);
        Assert.NotNull(persisted!.Professional);
        Assert.NotNull(result.Professional);
        Assert.Equal(professionalTypeId, persisted.Professional.ProfessionalTypeId);
        Assert.Single(persisted.ClinicAssignments);
        Assert.True(persisted.ClinicAssignments.First().IsPrimary);
        Assert.Single(persisted.Professional.Specialties);
        Assert.Single(persisted.Professional.Licenses);
    }

    private static Employee BuildDetalle(
        Employee persisted,
        Guid organizationId,
        string organizationName,
        string professionalTypeName)
    {
        // Simula la re-lectura del repositorio con las navegaciones del DTO.
        var employee = new Employee
        {
            Id = persisted.Id,
            OrganizationId = persisted.OrganizationId,
            FirstName = persisted.FirstName,
            MiddleName = persisted.MiddleName,
            LastName = persisted.LastName,
            Email = persisted.Email,
            JobTitle = persisted.JobTitle,
            Department = persisted.Department,
            Status = persisted.Status,
            CreatedAt = persisted.CreatedAt,
            Organization = new Organization { Id = organizationId, Code = "mediquer", Name = organizationName },
            ClinicAssignments = persisted.ClinicAssignments
                .Select(a => new EmployeeClinic
                {
                    EmployeeId = a.EmployeeId,
                    ClinicId = a.ClinicId,
                    IsPrimary = a.IsPrimary,
                    Status = a.Status,
                    Clinic = new Clinic { Id = a.ClinicId, Name = "Clínica Centro", OrganizationId = organizationId },
                })
                .ToList(),
            Professional = persisted.Professional is null
                ? null
                : new Professional
                {
                    Id = persisted.Professional.Id,
                    EmployeeId = persisted.Professional.EmployeeId,
                    ProfessionalTypeId = persisted.Professional.ProfessionalTypeId,
                    Bio = persisted.Professional.Bio,
                    ProfessionalType = new ProfessionalType
                    {
                        Id = persisted.Professional.ProfessionalTypeId!.Value,
                        Code = "PHYSICIAN",
                        Name = professionalTypeName,
                    },
                    Specialties = persisted.Professional.Specialties
                        .Select(s => new ProfessionalSpecialty
                        {
                            ProfessionalId = s.ProfessionalId,
                            SpecialtyId = s.SpecialtyId,
                            IsPrimary = s.IsPrimary,
                        })
                        .ToList(),
                    Licenses = persisted.Professional.Licenses
                        .Select(l => new ProfessionalLicense
                        {
                            Id = l.Id,
                            ProfessionalId = l.ProfessionalId,
                            LicenseType = l.LicenseType,
                            Number = l.Number,
                            VerificationStatus = l.VerificationStatus,
                        })
                        .ToList(),
                },
        };

        return employee;
    }
}

public class OrganizationCommandHandlerTests
{
    private readonly IOrganizationRepository _repository = Substitute.For<IOrganizationRepository>();
    private readonly ILogger<CreateOrganizationCommandHandler> _logger =
        Substitute.For<ILogger<CreateOrganizationCommandHandler>>();

    [Fact]
    public async Task Handle_CodigoDuplicado_LanzaBusinessRuleViolation()
    {
        _repository.ListOrganizationsAsync(Arg.Any<CancellationToken>())
            .Returns([new Organization { Id = Guid.NewGuid(), Code = "MEDIQUER", Name = "Existente" }]);

        var handler = new CreateOrganizationCommandHandler(_repository, _logger);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            handler.Handle(new CreateOrganizationCommand("mediquer", "MediQuer"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CodigoNuevo_CreaOrganizacion()
    {
        _repository.ListOrganizationsAsync(Arg.Any<CancellationToken>())
            .Returns([]);

        Organization? persisted = null;
        _repository.AddOrganizationAsync(Arg.Do<Organization>(o => persisted = o), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Organization>());

        var handler = new CreateOrganizationCommandHandler(_repository, _logger);

        var result = await handler.Handle(
            new CreateOrganizationCommand("mediquer", "MediQuer"), CancellationToken.None);

        Assert.NotNull(persisted);
        Assert.Equal("mediquer", persisted!.Code);
        Assert.Equal("MediQuer", persisted.Name);
        Assert.Equal("mediquer", result.Code);
    }
}
