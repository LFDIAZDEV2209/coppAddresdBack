using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Reglas compartidas de sala/sesión (SessionSupport): autorización por
/// identidad + supervisión, ventana de acceso y creación de sala.
/// </summary>
public class SessionSupportTests
{
    private readonly FakeReferenceDataService _referenceData = new();

    [Fact]
    public async Task RequireParticipant_ProfesionalDeLaCita_DevuelveProfessional()
    {
        var appointment = TestData.Appointment();
        _referenceData.Professionals[appointment.ProfessionalId] = TestData.Professional(userId: TestData.UserId);
        _referenceData.UserToProfessional[TestData.UserId] = appointment.ProfessionalId;

        var role = await SessionSupport.RequireParticipantAsync(
            _referenceData, appointment, TestData.UserId, hasManagePermission: false, CancellationToken.None);

        Assert.Equal(SessionParticipant.Professional, role);
    }

    [Fact]
    public async Task RequireParticipant_PacienteDeLaCita_DevuelvePatient()
    {
        var appointment = TestData.Appointment();
        _referenceData.Patients[appointment.PatientId] = TestData.Patient();
        _referenceData.UserToPatient[TestData.PatientUserId] = appointment.PatientId;

        var role = await SessionSupport.RequireParticipantAsync(
            _referenceData, appointment, TestData.PatientUserId, hasManagePermission: false, CancellationToken.None);

        Assert.Equal(SessionParticipant.Patient, role);
    }

    [Fact]
    public async Task RequireParticipant_Supervisor_DevuelveSupervisorSinConsultarIdentidad()
    {
        var appointment = TestData.Appointment();

        var role = await SessionSupport.RequireParticipantAsync(
            _referenceData, appointment, TestData.UserId, hasManagePermission: true, CancellationToken.None);

        Assert.Equal(SessionParticipant.Supervisor, role);
    }

    [Fact]
    public async Task RequireParticipant_UsuarioAjeno_LanzaForbidden()
    {
        var appointment = TestData.Appointment();
        var stranger = Guid.Parse("99999999-9999-9999-9999-999999999999");

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            SessionSupport.RequireParticipantAsync(
                _referenceData, appointment, stranger, hasManagePermission: false, CancellationToken.None));
    }

    [Fact]
    public async Task RequireSessionOwner_Paciente_LanzaForbidden()
    {
        var appointment = TestData.Appointment();
        _referenceData.Patients[appointment.PatientId] = TestData.Patient();
        _referenceData.UserToPatient[TestData.PatientUserId] = appointment.PatientId;

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            SessionSupport.RequireSessionOwnerAsync(
                _referenceData, appointment, TestData.PatientUserId, hasManagePermission: false, CancellationToken.None));
    }

    [Fact]
    public async Task RequireSessionOwner_ProfesionalDeLaCita_NoLanza()
    {
        var appointment = TestData.Appointment();
        _referenceData.Professionals[appointment.ProfessionalId] = TestData.Professional(userId: TestData.UserId);
        _referenceData.UserToProfessional[TestData.UserId] = appointment.ProfessionalId;

        await SessionSupport.RequireSessionOwnerAsync(
            _referenceData, appointment, TestData.UserId, hasManagePermission: false, CancellationToken.None);
    }

    [Fact]
    public void Window_AbreYcierraSegunSettings()
    {
        var appointment = TestData.Appointment();
        var settings = TestData.Settings();

        var (open, close) = SessionSupport.Window(appointment, settings);

        Assert.Equal(appointment.ScheduledStart.AddMinutes(-settings.RoomOpenBeforeMinutes), open);
        Assert.Equal(appointment.ScheduledEnd.AddMinutes(settings.RoomCloseAfterMinutes), close);
    }

    [Theory]
    [InlineData(AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.InProgress)]
    public void EnsureCanStartOrJoin_EstadosValidos_NoLanza(AppointmentStatus status)
        => SessionSupport.EnsureCanStartOrJoin(status);

    [Theory]
    [InlineData(AppointmentStatus.Requested)]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public void EnsureCanStartOrJoin_EstadosInvalidos_Lanza(AppointmentStatus status)
        => Assert.Throws<BusinessRuleViolationException>(() =>
            SessionSupport.EnsureCanStartOrJoin(status));

    [Fact]
    public void EnsureWithinWindow_AntesDeAbrir_Lanza()
    {
        var appointment = TestData.Appointment();
        var settings = TestData.Settings();
        var (open, _) = SessionSupport.Window(appointment, settings);

        Assert.Throws<BusinessRuleViolationException>(() =>
            SessionSupport.EnsureWithinWindow(appointment, settings, open.AddMinutes(-1)));
    }

    [Fact]
    public void EnsureWithinWindow_DespuesDeCerrar_Lanza()
    {
        var appointment = TestData.Appointment();
        var settings = TestData.Settings();
        var (_, close) = SessionSupport.Window(appointment, settings);

        Assert.Throws<BusinessRuleViolationException>(() =>
            SessionSupport.EnsureWithinWindow(appointment, settings, close));
    }

    [Fact]
    public void EnsureWithinWindow_DentroDeVentana_NoLanza()
    {
        var appointment = TestData.Appointment();
        var settings = TestData.Settings();
        var (open, close) = SessionSupport.Window(appointment, settings);

        SessionSupport.EnsureWithinWindow(appointment, settings, open.AddMinutes(1));
        SessionSupport.EnsureWithinWindow(appointment, settings, close.AddMinutes(-1));
    }

    [Fact]
    public void ProviderRoomName_EsDeterminista()
    {
        var id = Guid.NewGuid();
        Assert.Equal($"apt-{id:N}", SessionSupport.ProviderRoomName(id));
        Assert.Equal(SessionSupport.ProviderRoomName(id), SessionSupport.ProviderRoomName(id));
    }

    [Fact]
    public void NewRoom_AsignaVentanaYLimites()
    {
        var appointment = TestData.Appointment();
        var settings = TestData.Settings();
        var (open, close) = SessionSupport.Window(appointment, settings);

        var room = SessionSupport.NewRoom(appointment, settings, "apt-x", "RM123", TestData.UserId);

        Assert.Equal(appointment.Id, room.AppointmentId);
        Assert.Equal("apt-x", room.ProviderRoomName);
        Assert.Equal("RM123", room.ProviderRoomSid);
        Assert.Equal(open, room.ScheduledOpenAt);
        Assert.Equal(close, room.ScheduledCloseAt);
        Assert.Equal(settings.MaxParticipants, room.MaxParticipants);
        Assert.Equal(TestData.UserId, room.CreatedBy);
    }

    [Fact]
    public void Settings_DefaultMaxParticipants_Es3()
        => Assert.Equal(3, new TelemedicineSettings().MaxParticipants);

    [Fact]
    public void NewRoom_SettingsPorDefecto_AsignaCapacidad3()
    {
        var room = SessionSupport.NewRoom(
            TestData.Appointment(), new TelemedicineSettings(), "apt-x", "RM123", TestData.UserId);

        Assert.Equal(3, room.MaxParticipants);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(10)]
    public void EnsureValidMaxParticipants_EnRango_NoLanza(int value)
        => SessionSupport.EnsureValidMaxParticipants(value);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(11)]
    [InlineData(50)]
    public void EnsureValidMaxParticipants_FueraDeRango_Lanza(int value)
        => Assert.Throws<BusinessRuleViolationException>(() =>
            SessionSupport.EnsureValidMaxParticipants(value));

    [Theory]
    [InlineData(5)]
    [InlineData(60)]
    [InlineData(1440)]
    public void EnsureValidReopenGraceMinutes_EnRango_NoLanza(int value)
        => SessionSupport.EnsureValidReopenGraceMinutes(value);

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(1441)]
    public void EnsureValidReopenGraceMinutes_FueraDeRango_Lanza(int value)
        => Assert.Throws<BusinessRuleViolationException>(() =>
            SessionSupport.EnsureValidReopenGraceMinutes(value));

    [Fact]
    public void Settings_DefaultReopenGraceMinutes_Es60()
        => Assert.Equal(60, new TelemedicineSettings().ReopenGraceMinutes);

    [Theory]
    [InlineData(AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.InProgress)]
    [InlineData(AppointmentStatus.Completed)]
    public void EnsureChatAllowed_EstadosValidos_NoLanza(AppointmentStatus status)
        => SessionSupport.EnsureChatAllowed(status);

    [Theory]
    [InlineData(AppointmentStatus.Requested)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public void EnsureChatAllowed_EstadosInvalidos_Lanza(AppointmentStatus status)
        => Assert.Throws<BusinessRuleViolationException>(() =>
            SessionSupport.EnsureChatAllowed(status));
}
