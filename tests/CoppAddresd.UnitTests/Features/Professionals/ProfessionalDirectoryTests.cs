using CoppAddresd.Application.Features.Professionals;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Cache;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.Professionals;

/// <summary>
/// Directorio con filtros (2.2): el filtro por rol se resuelve via el Auth
/// Service (users-by-role) y se traduce a userIds; el filtro por especialidad
/// viaja al repositorio.
/// </summary>
public class ListEmployeesQueryHandlerTests
{
    private readonly IEmployeeRepository _repository = Substitute.For<IEmployeeRepository>();
    private readonly IAuthUsersByRoleClient _usersByRole = Substitute.For<IAuthUsersByRoleClient>();

    private ListEmployeesQueryHandler CreateHandler() => new(_repository, _usersByRole);

    [Fact]
    public async Task Handle_ConRoleId_ResuelveUserIdsYLosPasaAlRepositorio()
    {
        var roleId = Guid.NewGuid();
        var userIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        _usersByRole.GetUserIdsByRoleAsync(roleId, Arg.Any<CancellationToken>()).Returns(userIds);
        _repository
            .ListAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<IReadOnlyList<Guid>?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((new List<Employee>(), 0));

        var handler = CreateHandler();
        await handler.Handle(
            new ListEmployeesQuery(1, 20, null, null, null, null, null, roleId),
            CancellationToken.None
        );

        await _usersByRole.Received(1).GetUserIdsByRoleAsync(roleId, Arg.Any<CancellationToken>());
        await _repository
            .Received(1)
            .ListAsync(1, 20, null, null, null, null, null, userIds, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SinRoleId_NoConsultaAuthService()
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
                Arg.Any<IReadOnlyList<Guid>?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((new List<Employee>(), 0));

        var handler = CreateHandler();
        await handler.Handle(
            new ListEmployeesQuery(1, 20, null, null, null, null, null, null),
            CancellationToken.None
        );

        await _usersByRole.DidNotReceiveWithAnyArgs().GetUserIdsByRoleAsync(default, default);
    }

    [Fact]
    public async Task Handle_PasaSpecialtyIdAlRepositorio()
    {
        var specialtyId = Guid.NewGuid();
        _repository
            .ListAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<IReadOnlyList<Guid>?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((new List<Employee>(), 0));

        var handler = CreateHandler();
        await handler.Handle(
            new ListEmployeesQuery(1, 20, null, null, null, null, specialtyId, null),
            CancellationToken.None
        );

        await _repository
            .Received(1)
            .ListAsync(
                1,
                20,
                null,
                null,
                null,
                null,
                specialtyId,
                Arg.Any<IReadOnlyList<Guid>?>(),
                Arg.Any<CancellationToken>()
            );
    }
}

/// <summary>
/// Stats del directorio (2.3): delega en el repositorio con el alcance
/// (clinica/organizacion) indicado.
/// </summary>
public class GetEmployeesStatsQueryHandlerTests
{
    [Fact]
    public async Task Handle_DelegaAlRepositorioConElAlcance()
    {
        var repository = Substitute.For<IEmployeeRepository>();
        var clinicId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var expected = new EmployeeStatsDto(5, 3, 1, 1, []);
        repository.GetStatsAsync(orgId, clinicId, Arg.Any<CancellationToken>()).Returns(expected);

        var handler = new GetEmployeesStatsQueryHandler(repository, new NoCacheService());
        var result = await handler.Handle(
            new GetEmployeesStatsQuery(orgId, clinicId),
            CancellationToken.None
        );

        Assert.Equal(5, result.Total);
        Assert.Equal(3, result.Active);
        Assert.Equal(1, result.Invited);
        Assert.Equal(1, result.Inactive);
        await repository.Received(1).GetStatsAsync(orgId, clinicId, Arg.Any<CancellationToken>());
    }
}

/// <summary>
/// CRUD de catalogos (2.4): codigos unicos normalizados en mayusculas e
/// inmutables en edicion; duplicado -> BusinessRuleViolation (409); el
/// desactivado es soft (IsActive), no borrado.
/// </summary>
public class ProfessionalCatalogCommandsTests
{
    private readonly IOrganizationRepository _repository =
        Substitute.For<IOrganizationRepository>();

    [Fact]
    public async Task CreateType_CodigoDuplicado_LanzaBusinessRuleViolation()
    {
        _repository
            .ProfessionalTypeCodeExistsAsync("FONOAUDIOLOGO", Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new CreateProfessionalTypeCommandHandler(_repository, new NoCacheService());

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            handler.Handle(
                new CreateProfessionalTypeCommand("fonoaudiologo", "Fonoaudiologo", null, 10),
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task CreateType_NormalizaCodigoAMayusculas_YPersiste()
    {
        _repository
            .AddProfessionalTypeAsync(Arg.Any<ProfessionalType>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<ProfessionalType>());

        var handler = new CreateProfessionalTypeCommandHandler(_repository, new NoCacheService());
        var created = await handler.Handle(
            new CreateProfessionalTypeCommand("fonoaudiologo", "Fonoaudiologo", null, 10),
            CancellationToken.None
        );

        Assert.Equal("FONOAUDIOLOGO", created.Code);
        Assert.True(created.IsActive);
        await _repository
            .Received(1)
            .ProfessionalTypeCodeExistsAsync("FONOAUDIOLOGO", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateType_CodigoConCaracteresInvalidos_Lanza()
    {
        var handler = new CreateProfessionalTypeCommandHandler(_repository, new NoCacheService());

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            handler.Handle(
                new CreateProfessionalTypeCommand("Fono audiólogo!", "Fonoaudiologo", null, 10),
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task UpdateType_CambioNombreYDesactivacion_NoTocaElCodigo()
    {
        var existing = new ProfessionalType
        {
            Id = Guid.NewGuid(),
            Code = "PHYSICIAN",
            Name = "Physician (MD/DO)",
            IsActive = true,
        };
        _repository
            .GetProfessionalTypeByIdAsync(existing.Id, Arg.Any<CancellationToken>())
            .Returns(existing);

        var handler = new UpdateProfessionalTypeCommandHandler(_repository, new NoCacheService());
        var updated = await handler.Handle(
            new UpdateProfessionalTypeCommand(existing.Id, "Medico", null, null, false),
            CancellationToken.None
        );

        Assert.NotNull(updated);
        Assert.Equal("PHYSICIAN", updated!.Code);
        Assert.Equal("Medico", updated.Name);
        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task UpdateType_Inexistente_DevuelveNull()
    {
        _repository
            .GetProfessionalTypeByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ProfessionalType?)null);

        var handler = new UpdateProfessionalTypeCommandHandler(_repository, new NoCacheService());
        var result = await handler.Handle(
            new UpdateProfessionalTypeCommand(Guid.NewGuid(), "X", null, null, null),
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateSpecialty_CodigoDuplicado_LanzaBusinessRuleViolation()
    {
        _repository
            .SpecialtyCodeExistsAsync("FONOAUDIOLOGIA", Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new CreateSpecialtyCommandHandler(_repository, new NoCacheService());

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            handler.Handle(
                new CreateSpecialtyCommand("fonoaudiologia", "Fonoaudiologia", "Terapia", null, 30),
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task UpdateSpecialty_DesactivadaSinBorrar()
    {
        var existing = new Specialty
        {
            Id = Guid.NewGuid(),
            Code = "CLINICAL_NUTRITION",
            Name = "Clinical Nutrition",
            Category = "Nutrición",
            IsActive = true,
        };
        _repository
            .GetSpecialtyByIdAsync(existing.Id, Arg.Any<CancellationToken>())
            .Returns(existing);

        var handler = new UpdateSpecialtyCommandHandler(_repository, new NoCacheService());
        var updated = await handler.Handle(
            new UpdateSpecialtyCommand(existing.Id, "Nutrición Clínica", null, null, null, false),
            CancellationToken.None
        );

        Assert.NotNull(updated);
        Assert.Equal("CLINICAL_NUTRITION", updated!.Code);
        Assert.Equal("Nutrición Clínica", updated.Name);
        Assert.False(updated.IsActive);
    }
}
