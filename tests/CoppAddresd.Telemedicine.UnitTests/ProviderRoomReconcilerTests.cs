using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Application.VideoProvider;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.Telemedicine.UnitTests;

public class ProviderRoomReconcilerTests
{
    private readonly FakeRoomRepository _rooms = new();
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeVideoProvider _video = new();
    private readonly FakeMetricsQueue _metrics = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;
    private ProviderRoomReconciler Create() => new(_rooms, _appointments, _video, _uow,
        NullLogger<ProviderRoomReconciler>.Instance, _metrics);

    private (Appointment Appointment, VirtualRoom Room, TelemedicineSession Session) Arrange(string status)
    {
        var appointment = TestData.Appointment(status: AppointmentStatus.InProgress, start: _now.AddDays(20));
        _appointments.Items.Add(appointment);
        var room = new VirtualRoom { AppointmentId = appointment.Id, ProviderRoomSid = "RM-reconcile",
            ProviderRoomName = "reconcile", Status = VirtualRoomStatus.Active };
        var session = new TelemedicineSession { AppointmentId = appointment.Id, RoomId = room.Id,
            Status = TelemedicineSessionStatus.Active, StartedAt = _now.AddDays(-6) };
        room.Sessions.Add(session);
        _rooms.Rooms.Add(room);
        _video.Room = new("RM-reconcile", "reconcile", status, session.StartedAt, _now.AddDays(-6).AddMinutes(30), 1800);
        return (appointment, room, session);
    }

    [Fact]
    public async Task CompletedProviderRoom_ClosesEarlySession_UsesActualEnd_AndIsIdempotent()
    {
        var (appointment, room, session) = Arrange("completed");
        Assert.Equal(1, await Create().ReconcileAsync(_now));
        Assert.Equal(AppointmentStatus.Completed, appointment.Status);
        Assert.Equal(_video.Room!.EndedAt, appointment.CompletedAt);
        Assert.Equal(VirtualRoomStatus.Ended, room.Status);
        Assert.Equal(TelemedicineSessionStatus.Ended, session.Status);
        Assert.Equal(1800, session.DurationSeconds);
        Assert.Equal("provider-reconciliation", session.EndReason);
        Assert.Equal(0, _video.CompleteRoomCalls);
        Assert.Equal(0, await Create().ReconcileAsync(_now));
        Assert.Single(_rooms.WebhookEvents);
        Assert.Equal(2, _metrics.Events.Count);
    }

    [Theory]
    [InlineData("in-progress")]
    [InlineData("failed")]
    [InlineData("unknown")]
    public async Task UnconfirmedProviderCompletion_DoesNotEndEvenVeryOldSession(string status)
    {
        var (appointment, _, session) = Arrange(status);
        Assert.Equal(0, await Create().ReconcileAsync(_now));
        Assert.Equal(AppointmentStatus.InProgress, appointment.Status);
        Assert.Equal(TelemedicineSessionStatus.Active, session.Status);
        Assert.Equal(0, _uow.Calls);
        Assert.Empty(_metrics.Events);
    }

    [Fact]
    public async Task MissingProviderRoom_IsNotProofOfCompletion()
    {
        var (_, _, session) = Arrange("completed");
        _video.Room = null;
        Assert.Equal(0, await Create().ReconcileAsync(_now));
        Assert.Equal(TelemedicineSessionStatus.Active, session.Status);
    }

    [Fact]
    public async Task PreviouslyProcessedWebhook_DoesNotEmitDuplicateMetrics()
    {
        var (_, room, _) = Arrange("completed");
        _rooms.WebhookEvents.Add(new() { EventType = "room-ended", RoomSid = room.ProviderRoomSid });
        Assert.Equal(0, await Create().ReconcileAsync(_now));
        Assert.Empty(_metrics.Events);
    }

    [Fact]
    public async Task Cancellation_StopsBeforeAnyMutation()
    {
        Arrange("completed");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => Create().ReconcileAsync(_now, cts.Token));
        Assert.Empty(_rooms.WebhookEvents);
        Assert.Empty(_metrics.Events);
    }
}
