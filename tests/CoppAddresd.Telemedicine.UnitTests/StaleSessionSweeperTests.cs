using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Application.VideoProvider;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Barrido de citas vencidas: cierra citas InProgress vencidas como NoShow (el
/// paciente nunca ingresó) o Completed (ingresó), y citas Confirmed que nunca
/// iniciaron sesión como NoShow. Completa la sala del proveedor best-effort y
/// respeta la gracia de la ventana de sala.
/// </summary>
public class StaleSessionSweeperTests
{
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeRoomRepository _rooms = new();
    private readonly FakeSettingsProvider _settings = new();
    private readonly FakeVideoProvider _video = new();

    private StaleSessionSweeper CreateSweeper() =>
        new(
            _appointments,
            _rooms,
            _settings,
            _video,
            NullLogger<StaleSessionSweeper>.Instance
        );

    private (Appointment Appointment, VirtualRoom Room, TelemedicineSession Session) ArrangeStale(
        bool patientJoined = false)
    {
        var now = DateTimeOffset.UtcNow;
        var appointment = TestData.Appointment(
            status: AppointmentStatus.InProgress,
            start: now.AddHours(-2)
        );
        _appointments.Items.Add(appointment);

        var session = new TelemedicineSession
        {
            AppointmentId = appointment.Id,
            Status = TelemedicineSessionStatus.Active,
            StartedAt = appointment.ScheduledStart,
        };
        var room = new VirtualRoom
        {
            AppointmentId = appointment.Id,
            ProviderRoomSid = "RM-stale",
            ProviderRoomName = $"apt-{appointment.Id:N}",
            Status = VirtualRoomStatus.Active,
            ScheduledOpenAt = appointment.ScheduledStart.AddMinutes(-10),
            ScheduledCloseAt = appointment.ScheduledEnd.AddMinutes(15),
            PatientJoinedAt = patientJoined ? now.AddMinutes(-90) : null,
        };
        room.Sessions.Add(session);
        _rooms.Rooms.Add(room);
        _video.Room = new RoomInfo(
            "RM-stale",
            room.ProviderRoomName,
            "in-progress",
            null,
            null,
            null
        );

        return (appointment, room, session);
    }

    [Fact]
    public async Task Sweep_SinIngresoDelPaciente_CierraComoNoShow()
    {
        var (appointment, room, session) = ArrangeStale();

        var closed = await CreateSweeper().SweepAsync(DateTimeOffset.UtcNow);

        Assert.Equal(1, closed);
        Assert.Equal(AppointmentStatus.NoShow, appointment.Status);
        Assert.Equal(VirtualRoomStatus.Ended, room.Status);
        Assert.Equal(TelemedicineSessionStatus.Ended, session.Status);
        Assert.Equal("stale-sweep", session.EndReason);
        Assert.Equal(1, _video.CompleteRoomCalls);
    }

    [Fact]
    public async Task Sweep_ConIngresoDelPaciente_CierraComoCompleted()
    {
        var (appointment, _, _) = ArrangeStale(patientJoined: true);

        var closed = await CreateSweeper().SweepAsync(DateTimeOffset.UtcNow);

        Assert.Equal(1, closed);
        Assert.Equal(AppointmentStatus.Completed, appointment.Status);
    }

    [Fact]
    public async Task Sweep_DentroDeLaGracia_NoCierra()
    {
        var now = DateTimeOffset.UtcNow;
        // Fin hace 10 min; la gracia por defecto es 15 min.
        var appointment = TestData.Appointment(
            status: AppointmentStatus.InProgress,
            start: now.AddMinutes(-40)
        );
        _appointments.Items.Add(appointment);

        var closed = await CreateSweeper().SweepAsync(now);

        Assert.Equal(0, closed);
        Assert.Equal(AppointmentStatus.InProgress, appointment.Status);
    }

    [Fact]
    public async Task Sweep_SalaInexistente_NoCompletaProveedor()
    {
        var (appointment, room, _) = ArrangeStale();
        _rooms.Rooms.Clear();
        _video.Room = null;

        var closed = await CreateSweeper().SweepAsync(DateTimeOffset.UtcNow);

        Assert.Equal(1, closed);
        Assert.Equal(AppointmentStatus.NoShow, appointment.Status);
        Assert.Equal(0, _video.CompleteRoomCalls);
        Assert.Equal(VirtualRoomStatus.Active, room.Status);
    }

    [Fact]
    public async Task Sweep_CitaConfirmadaSinSesion_CierraComoNoShow()
    {
        var now = DateTimeOffset.UtcNow;
        var appointment = TestData.Appointment(
            status: AppointmentStatus.Confirmed,
            start: now.AddHours(-2)
        );
        _appointments.Items.Add(appointment);

        var closed = await CreateSweeper().SweepAsync(now);

        Assert.Equal(1, closed);
        Assert.Equal(AppointmentStatus.NoShow, appointment.Status);
    }

    [Fact]
    public async Task Sweep_CitaConfirmadaConSesionPrevia_NoCierra()
    {
        var now = DateTimeOffset.UtcNow;
        var appointment = TestData.Appointment(
            status: AppointmentStatus.Confirmed,
            start: now.AddHours(-2)
        );
        _appointments.Items.Add(appointment);

        var room = new VirtualRoom
        {
            AppointmentId = appointment.Id,
            ProviderRoomSid = "RM-prev",
            ProviderRoomName = $"apt-{appointment.Id:N}",
            Status = VirtualRoomStatus.Ended,
            ScheduledOpenAt = appointment.ScheduledStart.AddMinutes(-10),
            ScheduledCloseAt = appointment.ScheduledEnd.AddMinutes(15),
        };
        room.Sessions.Add(
            new TelemedicineSession
            {
                AppointmentId = appointment.Id,
                Status = TelemedicineSessionStatus.Ended,
                StartedAt = appointment.ScheduledStart,
                EndedAt = appointment.ScheduledEnd,
            }
        );
        _rooms.Rooms.Add(room);

        var closed = await CreateSweeper().SweepAsync(now);

        Assert.Equal(0, closed);
        Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
    }

    [Fact]
    public async Task Sweep_CitaConfirmadaDentroDeLaGracia_NoCierra()
    {
        var now = DateTimeOffset.UtcNow;
        // Fin hace 10 min; la gracia por defecto es 15 min.
        var appointment = TestData.Appointment(
            status: AppointmentStatus.Confirmed,
            start: now.AddMinutes(-40)
        );
        _appointments.Items.Add(appointment);

        var closed = await CreateSweeper().SweepAsync(now);

        Assert.Equal(0, closed);
        Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
    }
}
