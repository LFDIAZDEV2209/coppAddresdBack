using System.Reflection;
using System.Security.Claims;
using CoppAddresd.Api.Authorization;
using CoppAddresd.Api.Context;
using CoppAddresd.Api.Controllers;
using CoppAddresd.Application.Features.Notifications;
using CoppAddresd.Application.Features.ProgramProgress.Commands.MarkNotificationRead;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.Notifications;
using CoppAddresd.Application.Features.ProgramProgress.Queries.ListNotifications;
using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using CoppAddresd.Api.Seeders;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoppAddresd.UnitTests.ProgramProgress.Notifications;

/// <summary>
/// Acceso del paciente móvil al centro de avisos in-app (Fase 11): solo
/// <c>[Authorize]</c> (sin <c>Program.View</c>), identidad del JWT vía
/// <c>IProgramActorContext</c>, alias <c>me/notifications</c>, marcado como
/// leída con anti-IDOR (ajena → 404) y registro de dispositivos FCM.
/// </summary>
public sealed class PatientNotificationsTests
{
    private readonly Guid _patientId = Guid.NewGuid();
    private readonly StubMediator _mediator = new();
    private readonly StubActorContext _actor = new();
    private readonly ProgramController _controller;

    public PatientNotificationsTests()
    {
        _actor.PatientProfileId = _patientId;
        _controller = new ProgramController(
            _mediator,
            NullLogger<ProgramController>.Instance,
            new ConfigurationBuilder().Build(),
            _actor,
            Substitute.For<IObjectStorageService>()
        );
    }

    [Fact]
    public async Task ListNotifications_PacienteAutenticado_DevuelveOkConUnreadCount()
    {
        var page = new PaginatedNotificationsResult(
            [
                new NotificationDto(
                    Guid.NewGuid(),
                    "streak_milestone",
                    "Racha",
                    "7 días",
                    "normal",
                    "push",
                    DateTime.UtcNow,
                    null
                ),
                new NotificationDto(
                    Guid.NewGuid(),
                    "hydration_reminder",
                    "Agua",
                    "Bebe agua",
                    "normal",
                    "push",
                    DateTime.UtcNow,
                    DateTime.UtcNow
                ),
            ],
            Total: 2,
            Page: 1,
            PageSize: 20,
            TotalPages: 1,
            UnreadCount: 1
        );
        _mediator.On<ListNotificationsQuery, PaginatedNotificationsResult>(_ => page);

        var result = await _controller.ListNotifications(ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<PaginatedNotificationsResult>(ok.Value);
        Assert.Equal(1, body.UnreadCount);
        Assert.Equal(2, body.Data.Count);
    }

    [Fact]
    public async Task ListNotifications_EnviaPatientIdDelJwt_NuncaDelCliente()
    {
        ListNotificationsQuery? captured = null;
        _mediator.On<ListNotificationsQuery, PaginatedNotificationsResult>(q =>
        {
            captured = q;
            return new PaginatedNotificationsResult([], 0, 1, 20, 0, 0);
        });

        await _controller.ListNotifications(page: 2, pageSize: 5, ct: CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(_patientId, captured!.PatientId);
        Assert.Equal(2, captured.Page);
        Assert.Equal(5, captured.PageSize);
    }

    [Fact]
    public async Task ListNotifications_SinPerfilPaciente_Devuelve404SinLlamarAlMediador()
    {
        _actor.PatientProfileId = null;

        var result = await _controller.ListNotifications(ct: CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.False(_mediator.Called);
    }

    [Fact]
    public void ListNotifications_NoExigePermisoAdministrativo_SoloAuthorize()
    {
        var method = typeof(ProgramController).GetMethod(
            nameof(ProgramController.ListNotifications)
        )!;
        Assert.Empty(
            method.GetCustomAttributes(typeof(RequirePermissionAttribute), inherit: false)
        );
    }

    [Fact]
    public void ListNotifications_ExponeAliasMeNotifications()
    {
        var method = typeof(ProgramController).GetMethod(
            nameof(ProgramController.ListNotifications)
        )!;
        var templates = method
            .GetCustomAttributes<HttpGetAttribute>(inherit: false)
            .Select(a => a.Template)
            .ToList();
        Assert.Contains("notifications", templates);
        Assert.Contains("me/notifications", templates);
    }

    [Fact]
    public async Task MarkNotificationRead_Propia_DevuelveNoContent()
    {
        var notificationId = Guid.NewGuid();
        MarkNotificationReadCommand? captured = null;
        _mediator.On<MarkNotificationReadCommand, bool>(c =>
        {
            captured = c;
            return true;
        });

        var result = await _controller.MarkNotificationRead(notificationId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.NotNull(captured);
        Assert.Equal(notificationId, captured!.NotificationId);
        Assert.Equal(_patientId, captured.PatientId);
    }

    [Fact]
    public async Task MarkNotificationRead_Ajena_Devuelve404AntiIdor()
    {
        _mediator.On<MarkNotificationReadCommand, bool>(_ => false);

        var result = await _controller.MarkNotificationRead(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void MarkNotificationRead_NoExigePermisoAdministrativo_SoloAuthorize()
    {
        var method = typeof(ProgramController).GetMethod(
            nameof(ProgramController.MarkNotificationRead)
        )!;
        Assert.Empty(
            method.GetCustomAttributes(typeof(RequirePermissionAttribute), inherit: false)
        );
    }

    [Fact]
    public async Task RegisterDevice_DelegaUserIdDelJwt()
    {
        var userId = Guid.NewGuid();
        RegisterDeviceTokenCommand? captured = null;
        _mediator.On<RegisterDeviceTokenCommand, DeviceTokenDto>(c =>
        {
            captured = c;
            return new DeviceTokenDto(
                Guid.NewGuid(),
                c.UserId,
                c.Token,
                "android",
                DateTimeOffset.UtcNow,
                null
            );
        });
        var controller = NotificationsWithUser(userId);

        var result = await controller.RegisterDevice(
            new RegisterDeviceTokenRequest("fcm-token-abc", "android"),
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(captured);
        Assert.Equal(userId, captured!.UserId);
        Assert.Equal("fcm-token-abc", captured.Token);
        Assert.Equal(userId, Assert.IsType<DeviceTokenDto>(ok.Value).UserId);
    }

    [Fact]
    public async Task RegisterDevice_TokenVacio_Devuelve400()
    {
        var controller = NotificationsWithUser(Guid.NewGuid());

        var result = await controller.RegisterDevice(
            new RegisterDeviceTokenRequest("  ", "android"),
            CancellationToken.None
        );

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.False(_mediator.Called);
    }

    [Fact]
    public async Task RegisterDevice_SinJwt_Devuelve401()
    {
        var controller = new NotificationsController(_mediator)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity()),
                },
            },
        };

        var result = await controller.RegisterDevice(
            new RegisterDeviceTokenRequest("fcm-token-abc", "android"),
            CancellationToken.None
        );

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task UnregisterDevice_Existente_DevuelveNoContent()
    {
        _mediator.On<UnregisterDeviceTokenCommand, bool>(_ => true);
        var controller = NotificationsWithUser(Guid.NewGuid());

        var result = await controller.UnregisterDevice("fcm-token-abc", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task UnregisterDevice_Inexistente_Devuelve404()
    {
        _mediator.On<UnregisterDeviceTokenCommand, bool>(_ => false);
        var controller = NotificationsWithUser(Guid.NewGuid());

        var result = await controller.UnregisterDevice("fcm-token-xyz", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    private NotificationsController NotificationsWithUser(Guid userId)
    {
        return new NotificationsController(_mediator)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                            "test"
                        )
                    ),
                },
            },
        };
    }

    private sealed class StubMediator : IMediator
    {
        private readonly Dictionary<Type, Func<object, object?>> _handlers = [];

        public bool Called { get; private set; }

        public void On<TRequest, TResponse>(Func<TRequest, TResponse> handler)
            where TRequest : IRequest<TResponse> =>
            _handlers[typeof(TRequest)] = request => handler((TRequest)request)!;

        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken = default
        )
        {
            if (_handlers.TryGetValue(request.GetType(), out var handler))
            {
                Called = true;
                return Task.FromResult((TResponse)handler(request)!);
            }

            throw new InvalidOperationException(
                $"StubMediator sin handler para {request.GetType().Name}. Registra On<,> en el test."
            );
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("El controller usa siempre la sobrecarga genérica.");

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest =>
            throw new NotSupportedException("El controller usa siempre la sobrecarga genérica.");

        public Task<TResponse> Send<TRequest, TResponse>(
            TRequest request,
            CancellationToken cancellationToken = default
        )
            where TRequest : IRequest<TResponse> =>
            Send((IRequest<TResponse>)request, cancellationToken);

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException("El controller no usa streams.");

        public IAsyncEnumerable<TResponse> CreateStream<TRequest, TResponse>(
            TRequest request,
            CancellationToken cancellationToken = default
        )
            where TRequest : IStreamRequest<TResponse> =>
            throw new NotSupportedException("El controller no usa streams.");

        public IAsyncEnumerable<object?> CreateStream(
            object request,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException("El controller no usa streams.");

        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class StubActorContext : IProgramActorContext
    {
        public Guid? UserId { get; set; } = Guid.NewGuid();

        public IReadOnlyList<string> Roles { get; set; } = [];

        public IReadOnlyList<Guid>? ScopedPatientIds { get; set; }

        public Guid? PatientProfileId { get; set; }

        public Task<Guid?> ResolvePatientProfileIdAsync(CancellationToken ct = default) =>
            Task.FromResult(PatientProfileId);

        public Task<Guid?> ResolveActiveEnrollmentIdAsync(CancellationToken ct = default) =>
            Task.FromResult<Guid?>(null);

        public Task<bool> EnrollmentBelongsToCurrentPatientAsync(
            Guid enrollmentId,
            CancellationToken ct = default
        ) => Task.FromResult(true);

        public Task<bool> ActorScopedToEnrollmentAsync(
            Guid enrollmentId,
            CancellationToken ct = default
        ) => Task.FromResult(true);

        public Task<IReadOnlyList<Guid>?> ResolveScopedPatientIdsAsync(
            CancellationToken ct = default
        ) => Task.FromResult(ScopedPatientIds);
    }
}

/// <summary>
/// Siembra demo del centro de avisos (Fase 11) en
/// <c>DevProgramSeeder.SeedDemoNotificationsAsync</c>: 3 notificaciones para
/// el paciente de prueba 55551234, idempotente por tipo, no-op sin paciente.
/// </summary>
public sealed class DemoNotificationSeedTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private sealed class FakeProvider(AppDbContext db) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(AppDbContext) ? db : null;
    }

    private static DevProgramSeeder BuildSeeder(AppDbContext db)
    {
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(new FakeProvider(db));
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);
        return new DevProgramSeeder(
            scopeFactory,
            new ConfigurationBuilder().Build(),
            NullLogger<DevProgramSeeder>.Instance
        );
    }

    private static Task InvokeSeedAsync(DevProgramSeeder seeder)
    {
        var method = typeof(DevProgramSeeder).GetMethod(
            "SeedDemoNotificationsAsync",
            BindingFlags.NonPublic | BindingFlags.Instance
        )!;
        return (Task)method.Invoke(seeder, [CancellationToken.None])!;
    }

    private static PatientProfile MakeTestPatient() =>
        new()
        {
            Id = Guid.NewGuid(),
            FirstName = "Luis",
            LastName = "Prueba Movil",
            DocumentNumber = "55551234",
            Status = "Activo",
        };

    [Fact]
    public async Task SeedDemoNotifications_InsertaLas3ParaElPacienteDePrueba()
    {
        using var db = CreateInMemoryDb();
        var patient = MakeTestPatient();
        db.PatientProfiles.Add(patient);
        await db.SaveChangesAsync();

        await InvokeSeedAsync(BuildSeeder(db));

        var notifications = await db
            .AppNotifications.Where(n => n.PatientId == patient.Id)
            .OrderBy(n => n.Type)
            .ToListAsync();
        Assert.Equal(3, notifications.Count);
        Assert.Equal(
            ["appointment_reminder", "hydration_reminder", "streak_milestone"],
            notifications.Select(n => n.Type).ToList()
        );
        Assert.Equal("high", notifications.Single(n => n.Type == "appointment_reminder").Priority);
        Assert.Equal("normal", notifications.Single(n => n.Type == "hydration_reminder").Priority);
        Assert.Equal("normal", notifications.Single(n => n.Type == "streak_milestone").Priority);
        Assert.All(
            notifications,
            n =>
            {
                Assert.Equal("push", n.Channel);
                Assert.Null(n.ReadAt);
                Assert.False(string.IsNullOrWhiteSpace(n.Title));
                Assert.False(string.IsNullOrWhiteSpace(n.Message));
            }
        );
    }

    [Fact]
    public async Task SeedDemoNotifications_EsIdempotentePorTipo()
    {
        using var db = CreateInMemoryDb();
        db.PatientProfiles.Add(MakeTestPatient());
        await db.SaveChangesAsync();
        var seeder = BuildSeeder(db);

        await InvokeSeedAsync(seeder);
        await InvokeSeedAsync(seeder);

        Assert.Equal(3, await db.AppNotifications.CountAsync());
    }

    [Fact]
    public async Task SeedDemoNotifications_RespetaTiposExistentes()
    {
        using var db = CreateInMemoryDb();
        var patient = MakeTestPatient();
        db.PatientProfiles.Add(patient);
        db.AppNotifications.Add(
            new Domain.Entities.ProgramProgress.AppNotification
            {
                Id = Guid.NewGuid(),
                PatientId = patient.Id,
                Type = "appointment_reminder",
                Title = "Preexistente",
                Message = "No duplicar",
                Priority = "high",
                Channel = "push",
                SentAt = DateTime.UtcNow,
            }
        );
        await db.SaveChangesAsync();

        await InvokeSeedAsync(BuildSeeder(db));

        Assert.Equal(3, await db.AppNotifications.CountAsync(n => n.PatientId == patient.Id));
        Assert.Equal(
            1,
            await db.AppNotifications.CountAsync(n =>
                n.PatientId == patient.Id && n.Type == "appointment_reminder"
            )
        );
    }

    [Fact]
    public async Task SeedDemoNotifications_SinPaciente_NoHaceNada()
    {
        using var db = CreateInMemoryDb();

        await InvokeSeedAsync(BuildSeeder(db));

        Assert.Equal(0, await db.AppNotifications.CountAsync());
    }
}
