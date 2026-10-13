using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Application.VideoProvider;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Infrastructure.Persistence;
using CoppAddresd.Telemedicine.Infrastructure.Repositories;
using CoppAddresd.Telemedicine.UnitTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.Telemedicine.IntegrationTests;

[Collection(TelemedicineTestCollection.Name)]
public class ProviderRoomReconcilerTests(TelemedicineTestDatabase database)
{
    private readonly TelemedicineTestContext _ctx = new(database.ConnectionString);

    [Fact]
    public async Task Reconcile_KeysetPages_All205Rows_AndConcurrentRunsAreIdempotent()
    {
        var now = DateTimeOffset.UtcNow;
        var ended = now.AddMinutes(-30);
        var ids = new List<Guid>();
        var prefix = $"RM-reconcile-{Guid.NewGuid():N}-";
        await using (var seed = _ctx.Create())
        {
            for (var i = 0; i < 205; i++)
            {
                var appointment = TestData.Appointment(status: AppointmentStatus.InProgress, start: now.AddDays(20));
                appointment.ProfessionalId = Guid.NewGuid();
                var room = new VirtualRoom { Appointment = appointment, AppointmentId = appointment.Id,
                    ProviderRoomSid = $"{prefix}{i}", ProviderRoomName = $"qa-{Guid.NewGuid():N}",
                    Status = VirtualRoomStatus.Active };
                room.Sessions.Add(new() { AppointmentId = appointment.Id, Status = TelemedicineSessionStatus.Active,
                    StartedAt = ended.AddMinutes(-30) });
                seed.Rooms.Add(room);
                ids.Add(room.Id);
            }
            await seed.SaveChangesAsync();
        }

        async Task<int> Run()
        {
            await using var db = _ctx.Create();
            return await new ProviderRoomReconciler(new RoomRepository(db), new AppointmentRepository(db),
                new CompletedProvider(prefix, ended), new TelemedicineUnitOfWork(db),
                NullLogger<ProviderRoomReconciler>.Instance).ReconcileAsync(now);
        }
        var counts = await Task.WhenAll(Run(), Run());
        Assert.Equal(205, counts.Sum());
        await using var verify = _ctx.Create();
        Assert.Equal(205, await verify.Rooms.CountAsync(r => ids.Contains(r.Id) && r.Status == VirtualRoomStatus.Ended));
        Assert.Equal(205, await verify.Sessions.CountAsync(s => ids.Contains(s.RoomId)
            && s.Status == TelemedicineSessionStatus.Ended && s.DurationSeconds == 1800));
        Assert.Equal(205, await verify.Appointments.CountAsync(a => a.Room != null && ids.Contains(a.Room.Id)
            && a.Status == AppointmentStatus.Completed));
    }

    private sealed class CompletedProvider(string prefix, DateTimeOffset ended) : IVideoProvider
    {
        public Task<RoomInfo?> GetRoomAsync(string sid, CancellationToken ct) =>
            Task.FromResult<RoomInfo?>(sid.StartsWith(prefix) ? new(sid, sid, "completed", ended.AddMinutes(-30), ended, 1800) : null);
        public Task<RoomInfo> CreateRoomAsync(RoomRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task CompleteRoomAsync(string sid, CancellationToken ct) => throw new InvalidOperationException("No debe desconectar salas");
        public Task UpdateRoomMaxParticipantsAsync(string sid, int max, CancellationToken ct) => throw new NotSupportedException();
        public Task<string> GenerateAccessTokenAsync(AccessTokenRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ParticipantInfo>> GetParticipantsAsync(string sid, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> ValidateWebhookSignatureAsync(WebhookValidationRequest request, CancellationToken ct) => throw new NotSupportedException();
    }
}
