using CoppAddresd.Api.Context;
using CoppAddresd.Api.Controllers;
using CoppAddresd.Application.Features.Measurements.Queries.GetMyMeasurements;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace CoppAddresd.UnitTests.Measurements;

/// <summary>
/// Pruebas del endpoint <c>GET /api/v1/me/measurements</c> (Fase 7, móvil):
/// 401 sin JWT, 404 sin perfil (el handler lanza <see cref="NotFoundException"/>
/// y el middleware lo traduce a 404), 200 con página vacía y 400 con PageSize
/// inválido (el handler lanza <see cref="ValidationException"/> y el
/// middleware lo traduce a 400). El MediatR mockeado delega al handler REAL
/// para ejercitar la validación y el ownership de T1.1, no solo el mock.
/// </summary>
public sealed class MeasurementsEndpointTests
{
    private static MyPatientProfileController BuildController(IMediator mediator, Guid? userId)
    {
        var current = Substitute.For<ICurrentContext>();
        current.UserId.Returns(userId);
        return new MyPatientProfileController(mediator, current);
    }

    private static void WireRealHandler(
        IMediator mediator,
        IPatientRepository patients,
        IClinicalMeasurementRepository catalog,
        IPatientMeasurementRepository paged
    )
    {
        var handler = new GetMyMeasurementsQueryHandler(patients, catalog, paged);
        mediator
            .Send(Arg.Any<GetMyMeasurementsQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
                handler.Handle(
                    callInfo.Arg<GetMyMeasurementsQuery>(),
                    callInfo.Arg<CancellationToken>()
                )
            );
    }

    [Fact]
    public async Task GetMyMeasurements_SinJwt_RetornaUnauthorized()
    {
        // Arrange: sin JWT el contexto no resuelve usuario.
        var mediator = Substitute.For<IMediator>();
        var controller = BuildController(mediator, userId: null);

        // Act.
        var result = await controller.GetMyMeasurements(null, null, null, CancellationToken.None);

        // Assert: 401 sin llegar a MediatR.
        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        await mediator
            .DidNotReceiveWithAnyArgs()
            .Send(Arg.Any<GetMyMeasurementsQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetMyMeasurements_SinPerfil_LanzaNotFound()
    {
        // Arrange: JWT válido pero sin fila en patient_profiles.
        var userId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        var patients = Substitute.For<IPatientRepository>();
        patients
            .GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((PatientProfile?)null);
        WireRealHandler(
            mediator,
            patients,
            Substitute.For<IClinicalMeasurementRepository>(),
            Substitute.For<IPatientMeasurementRepository>()
        );
        var controller = BuildController(mediator, userId);

        // Act + Assert: el handler lanza 404 (el middleware lo traduce a
        // 404, sin revelar otros perfiles).
        await Assert.ThrowsAsync<NotFoundException>(() =>
            controller.GetMyMeasurements(null, null, null, CancellationToken.None)
        );
        await mediator
            .Received(1)
            .Send(
                Arg.Is<GetMyMeasurementsQuery>(q => q.UserId == userId),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task GetMyMeasurements_PaginaVacia_RetornaOkConItemsVacios()
    {
        // Arrange: paciente con perfil pero sin mediciones.
        var userId = Guid.NewGuid();
        var patient = new PatientProfile
        {
            Id = Guid.NewGuid(),
            FirstName = "Ana",
            LastName = "Ruiz",
            Status = "Activo",
        };
        var mediator = Substitute.For<IMediator>();
        var patients = Substitute.For<IPatientRepository>();
        patients.GetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(patient);
        var paged = Substitute.For<IPatientMeasurementRepository>();
        paged
            .GetPagedAsync(
                patient.Id,
                Arg.Any<string[]?>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new CursorPagedResult<MeasurementItemDto>([], NextCursor: null, HasNextPage: false)
            );
        WireRealHandler(
            mediator,
            patients,
            Substitute.For<IClinicalMeasurementRepository>(),
            paged
        );
        var controller = BuildController(mediator, userId);

        // Act.
        var result = await controller.GetMyMeasurements(null, null, null, CancellationToken.None);

        // Assert: 200 con items vacíos (el UserId del JWT viaja al query, anti-IDOR).
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var page = Assert.IsType<CursorPagedResult<MeasurementItemDto>>(ok.Value);
        Assert.Empty(page.Items);
        Assert.False(page.HasNextPage);
        Assert.Null(page.NextCursor);
        await mediator
            .Received(1)
            .Send(
                Arg.Is<GetMyMeasurementsQuery>(q => q.UserId == userId),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task GetMyMeasurements_PageSizeInvalido_LanzaValidation()
    {
        // Arrange: PageSize 0 falla en frontera (400) sin I/O.
        var userId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        var patients = Substitute.For<IPatientRepository>();
        patients
            .GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(
                new PatientProfile
                {
                    Id = Guid.NewGuid(),
                    FirstName = "Ana",
                    LastName = "Ruiz",
                    Status = "Activo",
                }
            );
        WireRealHandler(
            mediator,
            patients,
            Substitute.For<IClinicalMeasurementRepository>(),
            Substitute.For<IPatientMeasurementRepository>()
        );
        var controller = BuildController(mediator, userId);

        // Act + Assert: el handler lanza 400 (el middleware lo traduce a 400).
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            controller.GetMyMeasurements(0, null, null, CancellationToken.None)
        );
        Assert.Equal("pageSize", ex.Errors.First().PropertyName);
    }
}
