using CoppAddresd.Application.Features.Professionals;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace CoppAddresd.UnitTests.Features.Professionals;

public class PutProfessionalSchedulesCommandHandlerTests
{
    private readonly IEmployeeRepository _repository = Substitute.For<IEmployeeRepository>();

    private PutProfessionalSchedulesCommandHandler CreateHandler() => new(_repository);

    private static Employee BuildEmployeeWithProfessional(Guid professionalId)
    {
        return new Employee
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            FirstName = "Ana",
            LastName = "López",
            Email = "ana@test.com",
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            Professional = new Professional
            {
                Id = professionalId,
                EmployeeId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
            },
        };
    }

    [Fact]
    public async Task Handle_ProfesionalExistente_ReemplazaHorarios()
    {
        var professionalId = Guid.NewGuid();
        var employee = BuildEmployeeWithProfessional(professionalId);

        _repository.GetByProfessionalIdAsync(professionalId, Arg.Any<CancellationToken>())
            .Returns(employee);

        var handler = CreateHandler();

        var command = new PutProfessionalSchedulesCommand(
            professionalId,
            [
                new ScheduleSlotInput(1, "08:00", "17:00"),
                new ScheduleSlotInput(2, "08:00", "17:00"),
                new ScheduleSlotInput(3, "08:00", "17:00"),
            ]
        );

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(Unit.Value, result);

        await _repository.Received(1).ReplaceSchedulesAsync(
            professionalId,
            Arg.Is<IReadOnlyList<ProfessionalSchedule>>(s =>
                s.Count == 3 &&
                s[0].Weekday == 1 &&
                s[0].StartTime == new TimeOnly(8, 0) &&
                s[0].EndTime == new TimeOnly(17, 0) &&
                s[1].Weekday == 2 &&
                s[2].Weekday == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ListaVacia_ReemplazaConColeccionVacia()
    {
        var professionalId = Guid.NewGuid();
        var employee = BuildEmployeeWithProfessional(professionalId);

        _repository.GetByProfessionalIdAsync(professionalId, Arg.Any<CancellationToken>())
            .Returns(employee);

        var handler = CreateHandler();

        var command = new PutProfessionalSchedulesCommand(professionalId, []);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(Unit.Value, result);

        await _repository.Received(1).ReplaceSchedulesAsync(
            professionalId,
            Arg.Is<IReadOnlyList<ProfessionalSchedule>>(s => s.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ProfesionalInexistente_LanzaUnprocessableEntity()
    {
        var professionalId = Guid.NewGuid();

        _repository.GetByProfessionalIdAsync(professionalId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        var handler = CreateHandler();

        var command = new PutProfessionalSchedulesCommand(
            professionalId,
            [new ScheduleSlotInput(1, "08:00", "17:00")]
        );

        await Assert.ThrowsAsync<UnprocessableEntityException>(
            () => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_EmpleadoSinExtension_LanzaUnprocessableEntity()
    {
        var professionalId = Guid.NewGuid();
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            FirstName = "Ana",
            LastName = "López",
            Email = "ana@test.com",
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            Professional = null, // Sin extensión profesional
        };

        _repository.GetByProfessionalIdAsync(professionalId, Arg.Any<CancellationToken>())
            .Returns(employee);

        var handler = CreateHandler();

        var command = new PutProfessionalSchedulesCommand(
            professionalId,
            [new ScheduleSlotInput(1, "08:00", "17:00")]
        );

        await Assert.ThrowsAsync<UnprocessableEntityException>(
            () => handler.Handle(command, CancellationToken.None));
    }
}

public class PutProfessionalSchedulesCommandValidatorTests
{
    private readonly PutProfessionalSchedulesCommandValidator _validator = new();

    [Fact]
    public void Valid_SinHorarios_EsValido()
    {
        var command = new PutProfessionalSchedulesCommand(Guid.NewGuid(), []);
        var result = _validator.Validate(command);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Valid_HorariosNormales_EsValido()
    {
        var command = new PutProfessionalSchedulesCommand(
            Guid.NewGuid(),
            [
                new ScheduleSlotInput(1, "08:00", "17:00"),
                new ScheduleSlotInput(5, "09:00", "16:00"),
            ]
        );
        var result = _validator.Validate(command);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Invalid_ProfessionalIdVacio_Falla()
    {
        var command = new PutProfessionalSchedulesCommand(
            Guid.Empty,
            [new ScheduleSlotInput(1, "08:00", "17:00")]
        );
        var result = _validator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "ProfessionalId");
    }

    [Fact]
    public void Invalid_DiaFueraDeRango_Falla()
    {
        var command = new PutProfessionalSchedulesCommand(
            Guid.NewGuid(),
            [new ScheduleSlotInput(0, "08:00", "17:00")]
        );
        var result = _validator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("Weekday"));
    }

    [Fact]
    public void Invalid_DiaMayorASiete_Falla()
    {
        var command = new PutProfessionalSchedulesCommand(
            Guid.NewGuid(),
            [new ScheduleSlotInput(8, "08:00", "17:00")]
        );
        var result = _validator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("Weekday"));
    }

    [Fact]
    public void Invalid_FormatoHoraInvalido_Falla()
    {
        var command = new PutProfessionalSchedulesCommand(
            Guid.NewGuid(),
            [new ScheduleSlotInput(1, "invalido", "17:00")]
        );
        var result = _validator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("StartTime"));
    }

    [Fact]
    public void Invalid_HoraInicioMayorQueFin_Falla()
    {
        var command = new PutProfessionalSchedulesCommand(
            Guid.NewGuid(),
            [new ScheduleSlotInput(1, "17:00", "08:00")]
        );
        var result = _validator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("anterior"));
    }

    [Fact]
    public void Invalid_Duplicados_Falla()
    {
        var command = new PutProfessionalSchedulesCommand(
            Guid.NewGuid(),
            [
                new ScheduleSlotInput(1, "08:00", "17:00"),
                new ScheduleSlotInput(1, "09:00", "16:00"),
            ]
        );
        var result = _validator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Schedules");
    }
}

public class GetProfessionalSchedulesQueryHandlerTests
{
    private readonly IEmployeeRepository _repository = Substitute.For<IEmployeeRepository>();

    private GetProfessionalSchedulesQueryHandler CreateHandler() => new(_repository);

    [Fact]
    public async Task Handle_ProfesionalConHorarios_DevuelveDto()
    {
        var professionalId = Guid.NewGuid();
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            FirstName = "Ana",
            LastName = "López",
            Email = "ana@test.com",
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            Professional = new Professional
            {
                Id = professionalId,
                EmployeeId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
            },
        };

        _repository.GetByProfessionalIdAsync(professionalId, Arg.Any<CancellationToken>())
            .Returns(employee);

        _repository.GetSchedulesByProfessionalIdAsync(professionalId, Arg.Any<CancellationToken>())
            .Returns([
                new ProfessionalSchedule
                {
                    Id = Guid.NewGuid(),
                    ProfessionalId = professionalId,
                    Weekday = 1,
                    StartTime = new TimeOnly(8, 0),
                    EndTime = new TimeOnly(17, 0),
                },
                new ProfessionalSchedule
                {
                    Id = Guid.NewGuid(),
                    ProfessionalId = professionalId,
                    Weekday = 3,
                    StartTime = new TimeOnly(9, 0),
                    EndTime = new TimeOnly(15, 30),
                },
            ]);

        var handler = CreateHandler();

        var result = await handler.Handle(
            new GetProfessionalSchedulesQuery(professionalId),
            CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0].Weekday);
        Assert.Equal("08:00", result[0].StartTime);
        Assert.Equal("17:00", result[0].EndTime);
        Assert.Equal(3, result[1].Weekday);
        Assert.Equal("09:00", result[1].StartTime);
        Assert.Equal("15:30", result[1].EndTime);
    }

    [Fact]
    public async Task Handle_ProfesionalSinHorarios_DevuelveListaVacia()
    {
        var professionalId = Guid.NewGuid();
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            FirstName = "Ana",
            LastName = "López",
            Email = "ana@test.com",
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            Professional = new Professional
            {
                Id = professionalId,
                EmployeeId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
            },
        };

        _repository.GetByProfessionalIdAsync(professionalId, Arg.Any<CancellationToken>())
            .Returns(employee);

        _repository.GetSchedulesByProfessionalIdAsync(professionalId, Arg.Any<CancellationToken>())
            .Returns([]);

        var handler = CreateHandler();

        var result = await handler.Handle(
            new GetProfessionalSchedulesQuery(professionalId),
            CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_ProfesionalInexistente_DevuelveListaVacia()
    {
        var professionalId = Guid.NewGuid();

        _repository.GetByProfessionalIdAsync(professionalId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        var handler = CreateHandler();

        var result = await handler.Handle(
            new GetProfessionalSchedulesQuery(professionalId),
            CancellationToken.None);

        Assert.Empty(result);
    }
}
