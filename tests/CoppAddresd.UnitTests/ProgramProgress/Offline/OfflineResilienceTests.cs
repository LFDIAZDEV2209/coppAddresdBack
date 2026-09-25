using System.Reflection;
using CoppAddresd.Api.Context;
using CoppAddresd.Api.Controllers;
using CoppAddresd.Api.Http;
using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.ProgramProgress.Commands.CompleteTask;
using CoppAddresd.Application.Features.ProgramProgress.Commands.LogNutrition;
using CoppAddresd.Application.Features.ProgramProgress.Commands.MarkNotificationRead;
using CoppAddresd.Application.Features.ProgramProgress.Events;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.UnitTests.ProgramProgress.Handlers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.UnitTests.ProgramProgress.Offline;

/// <summary>
/// Resiliencia offline y en red débil (Fase 12): idempotencia en adherencia
/// diaria (tareas, hidratación, notificaciones), header
/// <c>X-Idempotency-Key</c>, sonda <c>ping</c> y anti-IDOR.
/// </summary>
public sealed class OfflineResilienceTests
{
    private static readonly DateOnly TestDate = new(2026, 9, 24);

    // -------------------------------------------------- Completado de tareas

    [Fact]
    public async Task CompleteTask_Replay_NoOtorgaXpNiEncolaMetricas()
    {
        // Primera escritura (Created, 80 XP) y replay idempotente: misma
        // respuesta, sin XP repetida ni eventos de métricas duplicados.
        var completionId = Guid.NewGuid();
        var created = new CompleteTaskResult(
            CompleteTaskOutcome.Created,
            completionId,
            80,
            80,
            false,
            0,
            0,
            0,
            80,
            750
        );
        var replay = created with { Outcome = CompleteTaskOutcome.Replay };

        var repository = new FakeProgramRepository { PatientToday = TestDate };
        var calls = 0;
        repository.OnCompleteTask = (_, _) => Task.FromResult(calls++ == 0 ? created : replay);
        var metrics = Substitute.For<IProgramMetricsQueue>();
        var handler = new CompleteTaskCommandHandler(
            repository,
            metrics,
            NullLogger<CompleteTaskCommandHandler>.Instance
        );

        var command = new CompleteTaskCommand(
            Guid.NewGuid(),
            TestDate,
            TaskCode.podcast,
            "offline-key-1",
            new DateTime(2026, 9, 24, 11, 14, 8, DateTimeKind.Utc),
            MoodScore: null,
            Barriers: null,
            ContentFingerprint: null
        );

        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(first, second);
        Assert.Equal(80, second.PointsAwarded);
        // Solo la primera escritura encola métricas (2 eventos); el replay, cero.
        await metrics
            .Received(2)
            .EnqueueAsync(Arg.Any<IProgramMetricEvent>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------- Hidratación

    [Fact]
    public async Task Hydration_RepeatActualizaVolumenSinDuplicarYConXp0()
    {
        var habitCheckId = Guid.NewGuid();
        var repository = new FakeProgramRepository
        {
            NutritionLogResult = new NutritionLogResultDto(
                habitCheckId,
                "agua",
                TestDate,
                true,
                0,
                120
            ),
        };
        var metrics = Substitute.For<IProgramMetricsQueue>();
        var handler = new LogNutritionCommandHandler(
            repository,
            metrics,
            NullLogger<LogNutritionCommandHandler>.Instance
        );

        var command = new LogNutritionCommand(
            Guid.NewGuid(),
            MealCode.agua,
            TestDate,
            Guid.NewGuid(),
            Intake: null
        );

        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        // Reintento offline: misma fila, XP 0, sin eventos de métricas.
        Assert.Equal(habitCheckId, first.HabitCheckId);
        Assert.Equal(habitCheckId, second.HabitCheckId);
        Assert.Equal(0, first.XpAwarded);
        Assert.Equal(0, second.XpAwarded);
        await metrics
            .DidNotReceiveWithAnyArgs()
            .EnqueueAsync(Arg.Any<IProgramMetricEvent>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------- Notificaciones (re-mark)

    [Fact]
    public async Task MarkNotificationRead_Repetido_Responde204()
    {
        var mediator = new StubMediator();
        mediator.On<MarkNotificationReadCommand, bool>(_ => true);
        var controller = ProgramWithMediator(mediator, Guid.NewGuid());
        var id = Guid.NewGuid();

        Assert.IsType<NoContentResult>(
            await controller.MarkNotificationRead(id, CancellationToken.None)
        );
        Assert.IsType<NoContentResult>(
            await controller.MarkNotificationRead(id, CancellationToken.None)
        );
    }

    [Fact]
    public async Task MarkNotificationRead_Ajena_Responde404AntiIdor()
    {
        var mediator = new StubMediator();
        mediator.On<MarkNotificationReadCommand, bool>(_ => false);
        var controller = ProgramWithMediator(mediator, Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(
            await controller.MarkNotificationRead(Guid.NewGuid(), CancellationToken.None)
        );
    }

    // -------------------------------------------------- X-Idempotency-Key

    [Fact]
    public async Task CompleteTask_HeaderIdempotencyKey_SeUsaCuandoBodyNoTraeClave()
    {
        CompleteTaskCommand? captured = null;
        var mediator = new StubMediator();
        mediator.On<CompleteTaskCommand, CompleteTaskResponseDto>(c =>
        {
            captured = c;
            return CompleteTaskResponseDto.FromResult(
                new CompleteTaskResult(
                    CompleteTaskOutcome.Created,
                    Guid.NewGuid(),
                    80,
                    80,
                    false,
                    0,
                    0,
                    0,
                    80,
                    750
                )
            );
        });
        var controller = ProgramWithMediator(mediator, Guid.NewGuid(), headerKey: "hdr-key-1");

        var result = await controller.CompleteTask(
            new CompleteTaskRequest(
                Guid.NewGuid(),
                TestDate,
                TaskCode.podcast,
                ClientRequestId: null,
                ClientCompletedAt: null,
                MoodScore: null,
                Barriers: null,
                ContentFingerprint: null
            ),
            CancellationToken.None
        );

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(captured);
        Assert.Equal("hdr-key-1", captured!.ClientRequestId);
    }

    [Fact]
    public async Task CompleteTask_BodyKey_TienePrioridadSobreHeader()
    {
        CompleteTaskCommand? captured = null;
        var mediator = new StubMediator();
        mediator.On<CompleteTaskCommand, CompleteTaskResponseDto>(c =>
        {
            captured = c;
            return CompleteTaskResponseDto.FromResult(
                new CompleteTaskResult(
                    CompleteTaskOutcome.Created,
                    Guid.NewGuid(),
                    80,
                    80,
                    false,
                    0,
                    0,
                    0,
                    80,
                    750
                )
            );
        });
        var controller = ProgramWithMediator(mediator, Guid.NewGuid(), headerKey: "hdr-key-1");

        await controller.CompleteTask(
            new CompleteTaskRequest(
                Guid.NewGuid(),
                TestDate,
                TaskCode.podcast,
                ClientRequestId: "body-key-9",
                ClientCompletedAt: null,
                MoodScore: null,
                Barriers: null,
                ContentFingerprint: null
            ),
            CancellationToken.None
        );

        Assert.Equal("body-key-9", captured!.ClientRequestId);
    }

    [Fact]
    public void IdempotencyKeys_ValoresInvalidos_SeIgnoran()
    {
        var request = new DefaultHttpContext().Request;

        Assert.Null(IdempotencyKeys.Resolve(request, null));

        request.Headers[IdempotencyKeys.HeaderName] = "   ";
        Assert.Null(IdempotencyKeys.Resolve(request, null));

        request.Headers[IdempotencyKeys.HeaderName] = new string('k', 65);
        Assert.Null(IdempotencyKeys.Resolve(request, null));

        request.Headers[IdempotencyKeys.HeaderName] = "  hdr-ok  ";
        Assert.Equal("hdr-ok", IdempotencyKeys.Resolve(request, null));
    }

    // -------------------------------------------------- Ping

    [Fact]
    public void Ping_RespondeOkConTimestampReciente()
    {
        var result = new HealthController().Ping();

        var ok = Assert.IsType<OkObjectResult>(result);
        var bodyType = ok.Value!.GetType();
        Assert.Equal("ok", bodyType.GetProperty("status")!.GetValue(ok.Value));
        var timestamp = (DateTime)bodyType.GetProperty("timestamp")!.GetValue(ok.Value)!;
        Assert.True((DateTime.UtcNow - timestamp).TotalSeconds < 60);
    }

    [Fact]
    public void Ping_EsAnonimoYRuteado()
    {
        Assert.Equal(
            "api/v1/health",
            typeof(HealthController)
                .GetCustomAttributes<RouteAttribute>(inherit: false)
                .Single()
                .Template
        );

        var method = typeof(HealthController).GetMethod(nameof(HealthController.Ping))!;
        Assert.Equal(
            "ping",
            method.GetCustomAttributes<HttpGetAttribute>(inherit: false).Single().Template
        );
        Assert.NotEmpty(
            method.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: false)
        );
        Assert.Empty(
            typeof(HealthController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
        );
    }

    // -------------------------------------------------- Stubs

    private static ProgramController ProgramWithMediator(
        StubMediator mediator,
        Guid patientId,
        string? headerKey = null
    )
    {
        var actor = new StubActorContext { PatientProfileId = patientId };
        var controller = new ProgramController(
            mediator,
            NullLogger<ProgramController>.Instance,
            new ConfigurationBuilder().Build(),
            actor,
            Substitute.For<IObjectStorageService>()
        );
        var httpContext = new DefaultHttpContext();
        if (headerKey is not null)
            httpContext.Request.Headers[IdempotencyKeys.HeaderName] = headerKey;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private sealed class StubMediator : IMediator
    {
        private readonly Dictionary<Type, Func<object, object?>> _handlers = [];

        public void On<TRequest, TResponse>(Func<TRequest, TResponse> handler)
            where TRequest : IRequest<TResponse> =>
            _handlers[typeof(TRequest)] = request => handler((TRequest)request)!;

        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken = default
        )
        {
            if (_handlers.TryGetValue(request.GetType(), out var handler))
                return Task.FromResult((TResponse)handler(request)!);
            throw new InvalidOperationException(
                $"StubMediator sin handler para {request.GetType().Name}."
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
