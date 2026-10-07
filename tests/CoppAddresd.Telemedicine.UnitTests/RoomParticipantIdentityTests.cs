using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Application.VideoProvider;
using CoppAddresd.Telemedicine.Domain.Entities;

namespace CoppAddresd.Telemedicine.UnitTests;

public class RoomParticipantIdentityTests
{
    [Fact]
    public async Task Handle_IdentidadesAuth_DistinguePacienteProfesionalYSupervisor()
    {
        var appointment = TestData.Appointment();
        var appointments = new FakeAppointmentRepository();
        appointments.Items.Add(appointment);
        var rooms = new FakeRoomRepository();
        rooms.Rooms.Add(new VirtualRoom { AppointmentId = appointment.Id, ProviderRoomSid = "RM-qa" });
        var references = new FakeReferenceDataService();
        references.Patients[appointment.PatientId] = TestData.Patient(userId: TestData.PatientUserId);
        references.Professionals[appointment.ProfessionalId] = TestData.Professional(userId: TestData.UserId);
        var provider = new FakeVideoProvider
        {
            Participants = [
                new ParticipantInfo("PA-patient", TestData.PatientUserId.ToString(), true, null, null),
                new ParticipantInfo("PA-professional", TestData.UserId.ToString(), true, null, null),
                new ParticipantInfo("PA-supervisor", Guid.NewGuid().ToString(), true, null, null),
            ],
        };
        var result = await new GetAppointmentRoomQueryHandler(appointments, rooms, provider, references)
            .Handle(new GetAppointmentRoomQuery(appointment.Id, Guid.NewGuid(), true), CancellationToken.None);
        Assert.Equal("Patient", result.Participants[0].Role);
        Assert.Equal("María Gómez", result.Participants[0].DisplayName);
        Assert.Equal("Professional", result.Participants[1].Role);
        Assert.Equal("Dra. Ana Pérez", result.Participants[1].DisplayName);
        Assert.Equal("Supervisor", result.Participants[2].Role);
        Assert.Null(result.Participants[2].DisplayName);
    }
}
