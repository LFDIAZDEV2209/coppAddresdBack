using CoppAddresd.Application.Features.ProgramProgress.Commands.PauseEnrollment;
using CoppAddresd.Application.Features.ProgramProgress.Commands.ResumeEnrollment;
using CoppAddresd.Application.Features.ProgramProgress.Commands.WithdrawEnrollment;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.UnitTests.ProgramProgress.Handlers;

/// <summary>
/// Tests de pausa/reanudación/retiro de inscripciones (SPEC §5.1 y §7.5): las
/// transiciones válidas devuelven el DTO con el estado nuevo y las inválidas
/// propagan 409 <c>INVALID_ENROLLMENT_STATE</c> / 404.
/// </summary>
public class EnrollmentStateHandlerTests
{
    private readonly FakeProgramRepository _repository = new();
    private readonly PauseEnrollmentCommandHandler _pauseHandler;
    private readonly ResumeEnrollmentCommandHandler _resumeHandler;
    private readonly WithdrawEnrollmentCommandHandler _withdrawHandler;
    private readonly Guid _enrollmentId = Guid.NewGuid();

    public EnrollmentStateHandlerTests()
    {
        _pauseHandler = new PauseEnrollmentCommandHandler(
            _repository, NullLogger<PauseEnrollmentCommandHandler>.Instance);
        _resumeHandler = new ResumeEnrollmentCommandHandler(
            _repository, NullLogger<ResumeEnrollmentCommandHandler>.Instance);
        _withdrawHandler = new WithdrawEnrollmentCommandHandler(
            _repository, NullLogger<WithdrawEnrollmentCommandHandler>.Instance);

        _repository.Enrollments[_enrollmentId] = new ProgramEnrollment
        {
            Id = _enrollmentId,
            PatientId = Guid.NewGuid(),
            TemplateId = Guid.NewGuid(),
            Timezone = "America/Bogota",
            Status = ProgramEnrollmentStatus.Active,
            StartLocalDate = new DateOnly(2026, 9, 21),
            CurrentWeekNumber = 1,
            CreatedAt = DateTime.UtcNow,
        };
    }

    [Fact]
    public async Task Handle_PausarActiva_DevuelvePaused()
    {
        var dto = await _pauseHandler.Handle(
            new PauseEnrollmentCommand(_enrollmentId, "Vacaciones"), CancellationToken.None);

        Assert.Equal(ProgramEnrollmentStatus.Paused, dto.Status);
        Assert.NotNull(dto.PausedAt);
        Assert.Equal(ProgramEnrollmentStatus.Paused, _repository.Enrollments[_enrollmentId].Status);
    }

    [Fact]
    public async Task Handle_PausarInexistente_LanzaNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _pauseHandler.Handle(new PauseEnrollmentCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ReanudarPausada_DevuelveActive()
    {
        _repository.Enrollments[_enrollmentId].Status = ProgramEnrollmentStatus.Paused;

        var dto = await _resumeHandler.Handle(
            new ResumeEnrollmentCommand(_enrollmentId), CancellationToken.None);

        Assert.Equal(ProgramEnrollmentStatus.Active, dto.Status);
        Assert.Null(dto.PausedAt);
    }

    [Fact]
    public async Task Handle_RetirarActiva_DevuelveWithdrawn()
    {
        var dto = await _withdrawHandler.Handle(
            new WithdrawEnrollmentCommand(_enrollmentId, "Derivación"), CancellationToken.None);

        Assert.Equal(ProgramEnrollmentStatus.Withdrawn, dto.Status);
        Assert.NotNull(dto.WithdrawnAt);
    }

    [Fact]
    public async Task Handle_RetirarInexistente_LanzaNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _withdrawHandler.Handle(new WithdrawEnrollmentCommand(Guid.NewGuid()), CancellationToken.None));
    }
}