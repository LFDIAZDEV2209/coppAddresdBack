using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.Telemedicine.UnitTests;

public class EarlyStartedRoomTests
{
    [Theory]
    [InlineData(AppointmentStatus.InProgress, TelemedicineSessionStatus.Active, true)]
    [InlineData(AppointmentStatus.InProgress, TelemedicineSessionStatus.Ended, false)]
    [InlineData(AppointmentStatus.Confirmed, TelemedicineSessionStatus.Active, false)]
    public async Task Handle_PacienteAntesDelHorario_SoloSesionActivaIniciadaPermiteEntrar(
        AppointmentStatus status, TelemedicineSessionStatus sessionStatus, bool allowed)
    {
        var now = DateTimeOffset.UtcNow;
        var appointment = TestData.Appointment(status: status, start: now.AddDays(1));
        var appointments = new FakeAppointmentRepository();
        appointments.Items.Add(appointment);
        var references = new FakeReferenceDataService();
        references.Patients[appointment.PatientId] = TestData.Patient();
        references.UserToPatient[TestData.PatientUserId] = appointment.PatientId;
        var rooms = new FakeRoomRepository();
        var settings = new FakeSettingsProvider();
        var room = new VirtualRoom
        {
            AppointmentId = appointment.Id,
            ProviderRoomName = $"apt-{appointment.Id:N}",
            ProviderRoomSid = "RM-qa",
            MaxParticipants = 10,
            ScheduledOpenAt = now.AddDays(1).AddMinutes(-15),
            ScheduledCloseAt = now.AddDays(1).AddHours(1),
            Sessions = [new TelemedicineSession { Status = sessionStatus, StartedAt = now.AddMinutes(-1) }],
        };
        rooms.Rooms.Add(room);
        var provider = new FakeVideoProvider();
        var handler = new JoinSessionCommandHandler(appointments, rooms, provider, references,
            settings, TestOptions.Create(), NullLogger<JoinSessionCommandHandler>.Instance);
        var command = new JoinSessionCommand(appointment.Id, TestData.PatientUserId, false);

        if (allowed)
        {
            var result = await handler.Handle(command, CancellationToken.None);
            Assert.Equal("fake-access-token", result.Token);
            var detail = await new GetAppointmentQueryHandler(appointments, references, settings, rooms)
                .Handle(new GetAppointmentQuery(appointment.Id), CancellationToken.None);
            Assert.Equal(room.Sessions.Single().StartedAt, detail.RoomOpensAt);
            Assert.Equal(room.ScheduledCloseAt, detail.RoomClosesAt);
            var mine = await new GetMyAppointmentsQueryHandler(appointments, references, settings, rooms)
                .Handle(new GetMyAppointmentsQuery(TestData.PatientUserId, null, null, null, 1, 20),
                    CancellationToken.None);
            Assert.Equal(detail.RoomOpensAt, Assert.Single(mine.Items).RoomOpensAt);
            Assert.Equal(detail.RoomClosesAt, Assert.Single(mine.Items).RoomClosesAt);

            // Una sesión activa no anula el límite de cierre.
            room.ScheduledCloseAt = now.AddMinutes(-1);
            await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
                handler.Handle(command, CancellationToken.None));
        }
        else
        {
            await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
                handler.Handle(command, CancellationToken.None));
        }
        Assert.Equal(0, provider.CreateRoomCalls);
    }
}
