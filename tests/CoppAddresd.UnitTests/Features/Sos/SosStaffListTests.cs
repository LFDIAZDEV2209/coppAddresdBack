using CoppAddresd.Api.Authorization;
using CoppAddresd.Api.Constants;
using CoppAddresd.Api.Context;
using CoppAddresd.Api.Controllers;
using CoppAddresd.Application.Features.Sos;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.Sos;

/// <summary>
/// Pruebas de la bandeja SOS del staff ERP (borrador del front): GET
/// /api/v1/sos/alerts?status=&amp;page=&amp;pageSize=. Cubre permiso staff,
/// scope (bypass de administración vs pacientes alcanzables) y delegación del
/// handler con la paginación estándar del repo.
/// </summary>
public sealed class SosStaffListTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ICurrentContext _context = Substitute.For<ICurrentContext>();
    private readonly ISosActorContext _actorContext = Substitute.For<ISosActorContext>();
    private readonly ISosAlertRepository _repository = Substitute.For<ISosAlertRepository>();

    private SosController NewController() => new(_mediator, _actorContext, _context, _repository);

    private void SetupPermission(bool hasPermission)
    {
        _context
            .HasPermissionAsync(PermissionCodes.SosAlertsManage, Arg.Any<CancellationToken>())
            .Returns(hasPermission);
    }

    private SosStaffActor Staff(bool bypass = false) =>
        new(
            Guid.NewGuid(),
            bypass ? null : Guid.NewGuid(),
            bypass ? null : Guid.NewGuid(),
            bypass ? null : Guid.NewGuid(),
            bypass
        );

    [Fact]
    public async Task List_SinPermisoSosAlertsManage_ForbidSinConsultarDatos()
    {
        // Staff autenticado (aud=erp garantizado por la política) pero sin el
        // permiso granular: 403 sin revelar nada ni tocar el repositorio.
        SetupPermission(hasPermission: false);

        var actionResult = await NewController().ListAlerts(ct: CancellationToken.None);

        Assert.IsType<ForbidResult>(actionResult.Result);
        await _mediator.DidNotReceive().Send(Arg.Any<object>(), Arg.Any<CancellationToken>());
        await _repository
            .DidNotReceive()
            .ResolveScopedPatientIdsAsync(
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task List_StatusInvalido_BadRequest()
    {
        SetupPermission(hasPermission: true);
        _actorContext
            .ResolveStaffActorAsync(Arg.Any<CancellationToken>())
            .Returns(Staff(bypass: true));

        var actionResult = await NewController()
            .ListAlerts(status: "no-es-un-estado", ct: CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        await _mediator.DidNotReceive().Send(Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_RolAdministrador_Bypass_EnviaPatientIdsNull()
    {
        // (D5) Admin/OrgAdmin/ClinicAdmin: listado global sin filtro de scope.
        SetupPermission(hasPermission: true);
        _actorContext
            .ResolveStaffActorAsync(Arg.Any<CancellationToken>())
            .Returns(Staff(bypass: true));

        SosAlertsPage? sentPage = null;
        _mediator
            .Send(
                Arg.Do<ListSosAlertsForStaffQuery>(q =>
                {
                    Assert.Null(q.PatientIds); // bypass: sin filtro
                    // Canonicalización del status: 'activa' → 'Activa'.
                    Assert.Equal("Activa", q.Status);
                    sentPage = new SosAlertsPage([], 0, 1, 20, 1);
                }),
                Arg.Any<CancellationToken>()
            )
            .Returns(c => sentPage!);

        var actionResult = await NewController()
            .ListAlerts(status: "activa", ct: CancellationToken.None);

        Assert.IsType<OkObjectResult>(actionResult.Result);
        await _repository
            .DidNotReceive()
            .ResolveScopedPatientIdsAsync(
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task List_ClinicoSinBypass_EnviaIdsResueltosPorScope()
    {
        // (D5) Clínico: pacientes de asignación directa ∪ clínica ∪ organización.
        SetupPermission(hasPermission: true);
        var staff = Staff(bypass: false);
        _actorContext.ResolveStaffActorAsync(Arg.Any<CancellationToken>()).Returns(staff);
        var resolved = new[] { Guid.NewGuid(), Guid.NewGuid() };
        _repository
            .ResolveScopedPatientIdsAsync(
                staff.ProfessionalId,
                staff.ActiveClinicId,
                staff.ActiveOrganizationId,
                Arg.Any<CancellationToken>()
            )
            .Returns(resolved);

        ListSosAlertsForStaffQuery? sentQuery = null;
        _mediator
            .Send(
                Arg.Do<ListSosAlertsForStaffQuery>(q => sentQuery = q),
                Arg.Any<CancellationToken>()
            )
            .Returns(new SosAlertsPage([], 0, 1, 20, 1));

        var actionResult = await NewController().ListAlerts(ct: CancellationToken.None);

        Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.NotNull(sentQuery);
        // La lista resuelta limita TODO el listado (nunca se abre el alcance).
        Assert.Equal(resolved, sentQuery!.PatientIds);
        await _repository
            .Received(1)
            .ResolveScopedPatientIdsAsync(
                staff.ProfessionalId,
                staff.ActiveClinicId,
                staff.ActiveOrganizationId,
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task List_ClinicoSinAlcance_ListaVaciaNiegaElListado()
    {
        // Clínico sin asignaciones ni contexto: el repositorio devuelve lista
        // vacía (denegar) y el listado no filtra silenciosamente a "todos".
        SetupPermission(hasPermission: true);
        _actorContext
            .ResolveStaffActorAsync(Arg.Any<CancellationToken>())
            .Returns(Staff(bypass: false));
        _repository
            .ResolveScopedPatientIdsAsync(
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns([]);

        ListSosAlertsForStaffQuery? sentQuery = null;
        _mediator
            .Send(
                Arg.Do<ListSosAlertsForStaffQuery>(q => sentQuery = q),
                Arg.Any<CancellationToken>()
            )
            .Returns(new SosAlertsPage([], 0, 1, 20, 1));

        await NewController().ListAlerts(ct: CancellationToken.None);

        Assert.NotNull(sentQuery);
        Assert.Empty(sentQuery!.PatientIds!);
    }

    // ---------- Handler: delegación y paginación ----------

    [Fact]
    public async Task Handler_DelegaAlRepositorioConEstadoCanonicoYPaginacionClamped()
    {
        var patientIds = new[] { Guid.NewGuid() };
        _repository
            .ListForStaffAsync(
                Arg.Any<IReadOnlyCollection<Guid>?>(),
                Arg.Any<string?>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                (
                    (IReadOnlyList<SosAlertListItemDto>)
                        new List<SosAlertListItemDto>
                        {
                            new(
                                Guid.NewGuid(),
                                patientIds[0],
                                "Sofía Vega",
                                "Activa",
                                DateTime.UtcNow,
                                null,
                                null,
                                null,
                                null,
                                "Pendiente",
                                "Pendiente",
                                "Pendiente"
                            ),
                        },
                    1
                )
            );

        var handler = new ListSosAlertsForStaffHandler(_repository);

        var result = await handler.Handle(
            new ListSosAlertsForStaffQuery(patientIds, "activa", Page: 2, PageSize: 500),
            CancellationToken.None
        );

        // Paginación estándar: pageSize clampeado a 100, page normalizado.
        Assert.Equal(2, result.Page);
        Assert.Equal(100, result.PageSize);
        Assert.Equal(1, result.Total);
        Assert.Equal(1, result.TotalPages);

        // DTO sin PII innecesaria: sin teléfono ni coordenadas en el listado.
        var row = Assert.Single(result.Data);
        Assert.Equal("Sofía Vega", row.PatientName);
        Assert.Equal("Activa", row.Status);

        // El handler delega el status VERBATIM (la canonicalización vive en el
        // controller) y la paginación ya saneada: pageSize clampeado a 100.
        await _repository
            .Received(1)
            .ListForStaffAsync(patientIds, "activa", 2, 100, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handler_PatientIdsNull_DelegaSinFiltroParaBypass()
    {
        _repository
            .ListForStaffAsync(null, null, 1, 20, Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<SosAlertListItemDto>)[], 0));

        var handler = new ListSosAlertsForStaffHandler(_repository);

        var result = await handler.Handle(
            new ListSosAlertsForStaffQuery(PatientIds: null),
            CancellationToken.None
        );

        Assert.Equal(0, result.Total);
        await _repository
            .Received(1)
            .ListForStaffAsync(null, null, 1, 20, Arg.Any<CancellationToken>());
    }
}
