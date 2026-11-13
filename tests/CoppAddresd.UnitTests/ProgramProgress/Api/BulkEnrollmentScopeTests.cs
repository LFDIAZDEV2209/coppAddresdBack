using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.ProgramProgress.Commands.BulkEnrollPatients;
using CoppAddresd.Application.Features.ProgramProgress.Commands.EnrollPatient;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.UnitTests.ProgramProgress.Api;

public sealed class BulkEnrollmentScopeTests
{
    [Fact]
    public async Task MixedPatients_ForeignIsOmittedBeforeDispatch_WithoutDisclosingData()
    {
        var own = Guid.NewGuid(); var foreign = Guid.NewGuid();
        var guard = Substitute.For<IPatientScopeGuard>();
        guard.EnsureAsync(foreign, "Program.Enroll", Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new NotFoundException("Recurso no encontrado.")));
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Is<EnrollPatientCommand>(c => c.PatientId == own), Arg.Any<CancellationToken>())
            .Returns(new ProgramEnrollmentDto(Guid.NewGuid(), own, Guid.NewGuid(), "America/Bogota", default,
                DateTime.UtcNow, new DateOnly(2026, 11, 13), 1, 12, 0, 0, 0, 0, null, null, null, DateTime.UtcNow));
        var handler = new BulkEnrollPatientsCommandHandler(mediator, NullLogger<BulkEnrollPatientsCommandHandler>.Instance, guard);
        var result = await handler.Handle(new([own, foreign], null, "America/Bogota", null), CancellationToken.None);
        Assert.Equal(1, result.Created); Assert.Equal(1, result.Failed);
        Assert.Equal("omitidos_sin_alcance", result.Results.Single(r => r.PatientId == foreign).Error);
        await mediator.DidNotReceive().Send(Arg.Is<EnrollPatientCommand>(c => c.PatientId == foreign), Arg.Any<CancellationToken>());
    }
}
