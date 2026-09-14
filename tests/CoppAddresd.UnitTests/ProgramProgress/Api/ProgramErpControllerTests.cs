using CoppAddresd.Api.Context;
using CoppAddresd.Api.Controllers;
using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.ProgramProgress.Commands.CreateBaseline;
using CoppAddresd.Application.Features.ProgramProgress.Commands.SetWeekContentRange;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.Erp;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.Scores;
using CoppAddresd.Application.Features.ProgramProgress.Queries.Erp;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetBaselines;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetEnrollment;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetSnapshot;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetXpLedger;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.UnitTests.ProgramProgress.Api;

/// <summary>
/// Tests de las acciones ERP nuevas del <see cref="ProgramController"/>
/// (TASK-04/05/05b/10/13b): resolución de 404 anti-IDOR (nunca 403), el
/// 404 del snapshot vía <see cref="NotFoundException"/> (middleware → 404)
/// y la propagación del actor/roles en los comandos. El mediador es un
/// stub que enruta por tipo de request (sin BD).
/// </summary>
public sealed class ProgramErpControllerTests
{
    private readonly StubMediator _mediator = new();
    private readonly StubActorContext _actor = new();
    private readonly ProgramController _controller;

    private readonly Guid _enrollmentId = Guid.NewGuid();

    public ProgramErpControllerTests()
    {
        _controller = new ProgramController(
            _mediator,
            NullLogger<ProgramController>.Instance,
            new ConfigurationBuilder().Build(),
            _actor,
            new StubObjectStorage());
    }

    // ------------------------------------------------------------ TASK-04: xp-ledger

    [Fact]
    public async Task GetXpLedger_ResultadoNull_Devuelve404()
    {
        _mediator.On<GetXpLedgerQuery, PaginatedXpLedgerResult?>(_ => null);

        var result = await _controller.GetXpLedger(_enrollmentId, ct: CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetXpLedger_ConResultado_DevuelveOkConLaPagina()
    {
        var page = new PaginatedXpLedgerResult([], Total: 0, Page: 1, PageSize: 20, TotalPages: 0);
        _mediator.On<GetXpLedgerQuery, PaginatedXpLedgerResult?>(_ => page);

        var result = await _controller.GetXpLedger(_enrollmentId, ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(page, ok.Value);
    }

    // ------------------------------------------------------------ TASK-05: baselines

    [Fact]
    public async Task GetBaselines_ResultadoNull_Devuelve404()
    {
        _mediator.On<GetBaselinesQuery, IReadOnlyList<ClinicalBaselineDto>?>(_ => null);

        var result = await _controller.GetBaselines(_enrollmentId, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetBaselines_ConResultado_DevuelveOkConLaLista()
    {
        var list = new List<ClinicalBaselineDto>();
        _mediator.On<GetBaselinesQuery, IReadOnlyList<ClinicalBaselineDto>?>(_ => list);

        var result = await _controller.GetBaselines(_enrollmentId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(list, ok.Value);
    }

    [Fact]
    public async Task CreateBaseline_SinUserId_Devuelve401()
    {
        _actor.UserId = null;

        var result = await _controller.CreateBaseline(
            _enrollmentId, new CreateBaselineRequest(Guid.NewGuid(), 80m, Guid.NewGuid(),
                FavorableDirection.LowerIsBetter, new DateOnly(2026, 8, 1), null),
            CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateBaseline_ConActor_EnviaComandoConRoles()
    {
        var userId = Guid.NewGuid();
        _actor.UserId = userId;
        _actor.Roles = ["Physician"];
        var dto = new ClinicalBaselineDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "weight", 80m,
            Guid.NewGuid(), "kg", FavorableDirection.LowerIsBetter, null,
            new DateOnly(2026, 8, 1), userId, DateTime.UtcNow);
        CreateBaselineCommand? captured = null;
        _mediator.On<CreateBaselineCommand, ClinicalBaselineDto>(cmd => { captured = cmd; return dto; });

        var result = await _controller.CreateBaseline(
            _enrollmentId, new CreateBaselineRequest(Guid.NewGuid(), 80m, Guid.NewGuid(),
                FavorableDirection.LowerIsBetter, new DateOnly(2026, 8, 1), 75m),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(dto, ok.Value);
        Assert.NotNull(captured);
        Assert.Equal(_enrollmentId, captured.EnrollmentId);
        Assert.Equal(userId, captured.ActorId);
        Assert.Equal(new[] { "Physician" }, captured.CallerRoles);
        Assert.Equal(75m, captured.TargetValue);
    }

    // ------------------------------------------------------------ TASK-05b: snapshot ERP

    [Fact]
    public async Task GetEnrollmentSnapshot_SinInscripcion_PropagaNotFound()
    {
        // El handler lanza NotFoundException (NO_ACTIVE_ENROLLMENT); la acción
        // NO la traga (el middleware global la convierte en 404).
        _mediator.On<GetSnapshotQuery, ProgramSnapshotDto>(_ =>
            throw new NotFoundException("NO_ACTIVE_ENROLLMENT: no existe la inscripción."));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _controller.GetEnrollmentSnapshot(_enrollmentId, CancellationToken.None));
    }

    [Fact]
    public async Task GetEnrollmentSnapshot_ConSnapshot_DevuelveOk()
    {
        var snapshot = SampleSnapshot(_enrollmentId);
        _mediator.On<GetSnapshotQuery, ProgramSnapshotDto>(_ => snapshot);

        var result = await _controller.GetEnrollmentSnapshot(_enrollmentId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(snapshot, ok.Value);
    }

    // ------------------------------------------------------------ TASK-10: enrollment detail

    [Fact]
    public async Task GetEnrollment_ResultadoNull_Devuelve404()
    {
        _mediator.On<GetEnrollmentQuery, ProgramEnrollmentDto?>(_ => null);

        var result = await _controller.GetEnrollment(_enrollmentId, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetEnrollment_ConResultado_DevuelveOkConCurrentLevel()
    {
        var dto = new ProgramEnrollmentDto(_enrollmentId, Guid.NewGuid(), Guid.NewGuid(), "America/Bogota",
            ProgramEnrollmentStatus.Active, DateTime.UtcNow, new DateOnly(2026, 1, 5), 12, 83, 1620,
            11, 27, 2, null, null, null, DateTime.UtcNow,
            CurrentLevel: "Constante");
        _mediator.On<GetEnrollmentQuery, ProgramEnrollmentDto?>(_ => dto);

        var result = await _controller.GetEnrollment(_enrollmentId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var value = Assert.IsType<ProgramEnrollmentDto>(ok.Value);
        Assert.Equal("Constante", value.CurrentLevel);
        Assert.Equal(1620, value.XpBalance);
    }

    // ------------------------------------------------------------ TASK-13b: content range

    [Fact]
    public async Task SetWeekContentRange_ConActor_EnviaRangoYDevuelveOk()
    {
        var userId = Guid.NewGuid();
        _actor.UserId = userId;
        var weeks = new List<ProgramContentWeekDto>
        {
            new(2, new DateOnly(2026, 1, 12), new DateOnly(2026, 1, 18), null, null),
            new(3, new DateOnly(2026, 1, 19), new DateOnly(2026, 1, 25), null, null),
        };
        SetWeekContentRangeCommand? captured = null;
        _mediator.On<SetWeekContentRangeCommand, IReadOnlyList<ProgramContentWeekDto>>(cmd =>
        {
            captured = cmd;
            return weeks;
        });

        var result = await _controller.SetWeekContentRange(
            _enrollmentId,
            new SetWeekContentRangeRequest(2, 3, null, null),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(weeks, ok.Value);
        Assert.NotNull(captured);
        Assert.Equal(_enrollmentId, captured.EnrollmentId);
        Assert.Equal(2, captured.FromWeek);
        Assert.Equal(3, captured.ToWeek);
        Assert.Equal(userId, captured.ActorId);
    }

    // ------------------------------------------------------------ UC-004: controls ERP

    [Fact]
    public async Task GetPatientControls_ResultadoNull_Devuelve404()
    {
        // El handler devuelve null sin inscripción activa; la acción lo mapea a 404.
        _mediator.On<GetPatientControlsQuery, PatientControlsDto?>(_ => null);

        var result = await _controller.GetPatientControls(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetPatientControls_ConResultado_DevuelveOkConElDto()
    {
        var patientId = Guid.NewGuid();
        // Alcance explícito que incluye al paciente pedido (clínico asignado).
        _actor.ScopedPatientIds = [patientId];
        var dto = SamplePatientControls();
        _mediator.On<GetPatientControlsQuery, PatientControlsDto?>(_ => dto);

        var result = await _controller.GetPatientControls(patientId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(dto, ok.Value);
    }

    [Fact]
    public async Task GetPatientControls_ActorFueraDeAlcance_Devuelve404SinConsultar()
    {
        // Alcance restringido a OTRO paciente: la acción corta antes del
        // mediador (sin handler registrado, una llamada lanzaría).
        _actor.ScopedPatientIds = [Guid.NewGuid()];

        var result = await _controller.GetPatientControls(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetPatientControls_AlcanceSinFiltroAdmin_DevuelveOk()
    {
        // Rol admin (ScopedPatientIds null = sin filtro): pasa como en los
        // endpoints T-81 que aplican el scoping.
        var patientId = Guid.NewGuid();
        _actor.ScopedPatientIds = null;
        var dto = SamplePatientControls();
        _mediator.On<GetPatientControlsQuery, PatientControlsDto?>(_ => dto);

        var result = await _controller.GetPatientControls(patientId, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    private static ProgramSnapshotDto SampleSnapshot(Guid enrollmentId) => new(
        enrollmentId,
        new ProgramSnapshotTemplateDto(
            Guid.NewGuid(), "default-83w", "Programa 83 semanas", 83, 12,
            ProgramWeekStatus.Active, new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 27),
            1, ["nut"]),
        new DateOnly(2026, 9, 24),
        [],
        0, false, 0,
        new XpInfoDto(1620, "Constante", 3000),
        new StreakInfoDto(11, 27, 2, 1.0m, null, 0),
        16,
        []);

    /// <summary>Read model de controles (UC-004) con un control completado con documento.</summary>
    private static PatientControlsDto SamplePatientControls()
    {
        var document = new PatientControlsDocumentDto(
            Guid.NewGuid(), "labs/exam.pdf", DateTime.UtcNow, 2,
            ["glucose_fasting", "hba1c"]);
        return new PatientControlsDto(
            new PatientControlsEnrollmentDto(
                Guid.NewGuid(), new DateOnly(2026, 1, 5), "America/Bogota", "active"),
            new PatientControlsCurrentControlDto(Guid.NewGuid(), 14, "sent", DateTime.UtcNow),
            new PatientControlsNextDueDto(21, new DateOnly(2026, 1, 25)),
            [
                new PatientControlsMilestoneDto(
                    7, new DateOnly(2026, 1, 11), "completed", Guid.NewGuid(),
                    DateTime.UtcNow, DateTime.UtcNow, null, DateTime.UtcNow, null, document),
                new PatientControlsMilestoneDto(
                    14, new DateOnly(2026, 1, 18), "sent", Guid.NewGuid(),
                    DateTime.UtcNow, null, null, null, null, null),
            ],
            new PatientControlsAdherenceDto(1, 0, 0, 1, 1, 0, 2));
    }

    // ------------------------------------------------------------ Stubs

    private sealed class StubMediator : IMediator
    {
        private readonly Dictionary<Type, Func<object, object?>> _handlers = [];

        public void On<TRequest, TResponse>(Func<TRequest, TResponse> handler)
            where TRequest : IRequest<TResponse>
            => _handlers[typeof(TRequest)] = request => handler((TRequest)request)!;

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (_handlers.TryGetValue(request.GetType(), out var handler))
            {
                return Task.FromResult((TResponse)handler(request)!);
            }

            throw new InvalidOperationException(
                $"StubMediator sin handler para {request.GetType().Name}. Registra On<,> en el test.");
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("El controller usa siempre la sobrecarga genérica.");

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
            => throw new NotSupportedException("El controller usa siempre la sobrecarga genérica.");

        public Task<TResponse> Send<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
            => Send((IRequest<TResponse>)request, cancellationToken);

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("El controller no usa streams.");

        public IAsyncEnumerable<TResponse> CreateStream<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IStreamRequest<TResponse>
            => throw new NotSupportedException("El controller no usa streams.");

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("El controller no usa streams.");

        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.CompletedTask;
    }

    private sealed class StubActorContext : IProgramActorContext
    {
        public Guid? UserId { get; set; } = Guid.NewGuid();

        public IReadOnlyList<string> Roles { get; set; } = [];

        /// <summary>
        /// Alcance de pacientes del actor (T-81): null = sin filtro (roles
        /// admin); lista = filtro IN (paciente/clínico).
        /// </summary>
        public IReadOnlyList<Guid>? ScopedPatientIds { get; set; }

        public Task<Guid?> ResolvePatientProfileIdAsync(CancellationToken ct = default)
            => Task.FromResult<Guid?>(Guid.NewGuid());

        public Task<Guid?> ResolveActiveEnrollmentIdAsync(CancellationToken ct = default)
            => Task.FromResult<Guid?>(null);

        public Task<bool> EnrollmentBelongsToCurrentPatientAsync(Guid enrollmentId, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<bool> ActorScopedToEnrollmentAsync(Guid enrollmentId, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<IReadOnlyList<Guid>?> ResolveScopedPatientIdsAsync(CancellationToken ct = default)
            => Task.FromResult(ScopedPatientIds);
    }

    private sealed class StubObjectStorage : IObjectStorageService
    {
        public bool IsCloudStorage => false;

        public Task<string> GetPreSignedUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default)
            => Task.FromResult(string.Empty);

        public Task<string> PutObjectAsync(string key, Stream content, string? contentType = null, CancellationToken ct = default)
            => Task.FromResult(key);

        public Task<Stream> GetObjectAsync(string key, CancellationToken ct = default)
            => throw new FileNotFoundException(key);

        public Task<CoppAddresd.Application.DTOs.Storage.ObjectMetadata?> HeadObjectAsync(string key, CancellationToken ct = default)
            => Task.FromResult<CoppAddresd.Application.DTOs.Storage.ObjectMetadata?>(null);

        public Task<CoppAddresd.Application.DTOs.Storage.ListObjectsResult> ListObjectsAsync(
            string prefix, string? continuationToken = null, CancellationToken ct = default)
            => Task.FromResult(new CoppAddresd.Application.DTOs.Storage.ListObjectsResult([], null));

        public Task DeleteObjectAsync(string key, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task DeleteObjectsAsync(IEnumerable<string> keys, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task CopyObjectAsync(string sourceKey, string destinationKey, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<string> GetPreSignedUploadUrlAsync(
            string key, string? contentType, TimeSpan expiry, string publicBaseUrl, CancellationToken ct = default)
            => Task.FromResult(key);
    }
}