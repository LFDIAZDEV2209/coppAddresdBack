using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Chat de la consulta (F3): autorización con la misma matriz de la sala
/// (profesional/paciente/supervisor OK, ajeno 403), guarda de estado
/// (Confirmed/InProgress/Completed; Requested y terminales sin atención →
/// 409), rol y emisor derivados del JWT y lectura ordenada por cursor.
/// </summary>
public class ChatHandlerTests
{
    private readonly FakeReferenceDataService _referenceData = new();
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeChatMessageRepository _messages = new();
    private readonly SendRoomChatMessageCommandHandler _send;
    private readonly GetRoomChatMessagesQueryHandler _list;

    public ChatHandlerTests()
    {
        _send = new SendRoomChatMessageCommandHandler(
            _appointments, _messages, _referenceData,
            NullLogger<SendRoomChatMessageCommandHandler>.Instance);
        _list = new GetRoomChatMessagesQueryHandler(_appointments, _messages, _referenceData);
    }

    private Appointment AddAppointment(AppointmentStatus status = AppointmentStatus.InProgress)
    {
        var appointment = TestData.Appointment(status: status);
        _appointments.Items.Add(appointment);
        return appointment;
    }

    private void AsProfessional(Appointment appointment)
    {
        _referenceData.Professionals[appointment.ProfessionalId] =
            TestData.Professional(userId: TestData.UserId);
        _referenceData.UserToProfessional[TestData.UserId] = appointment.ProfessionalId;
    }

    private void AsPatient(Appointment appointment)
    {
        _referenceData.Patients[appointment.PatientId] = TestData.Patient();
        _referenceData.UserToPatient[TestData.PatientUserId] = appointment.PatientId;
    }

    [Fact]
    public async Task Send_Profesional_DerivaEmisorYRolDelJwt()
    {
        var appointment = AddAppointment();
        AsProfessional(appointment);

        var dto = await _send.Handle(
            new SendRoomChatMessageCommand(appointment.Id, "  hola  ", TestData.UserId, false),
            CancellationToken.None);

        Assert.Equal(appointment.Id, dto.AppointmentId);
        Assert.Equal(TestData.UserId, dto.SenderUserId);
        Assert.Equal("Professional", dto.SenderRole);
        Assert.Equal("hola", dto.Body);
        Assert.Single(_messages.Items);
        Assert.Equal("hola", _messages.Items[0].Body);
    }

    [Fact]
    public async Task Send_Paciente_DerivaRolPatient()
    {
        var appointment = AddAppointment();
        AsPatient(appointment);

        var dto = await _send.Handle(
            new SendRoomChatMessageCommand(appointment.Id, "buenas", TestData.PatientUserId, false),
            CancellationToken.None);

        Assert.Equal("Patient", dto.SenderRole);
        Assert.Equal(TestData.PatientUserId, dto.SenderUserId);
    }

    [Fact]
    public async Task Send_Supervisor_DerivaRolSupervisor()
    {
        var appointment = AddAppointment();

        var dto = await _send.Handle(
            new SendRoomChatMessageCommand(appointment.Id, "superviso", Guid.NewGuid(), true),
            CancellationToken.None);

        Assert.Equal("Supervisor", dto.SenderRole);
    }

    [Fact]
    public async Task Send_UsuarioAjeno_LanzaForbiddenYSinPersistir()
    {
        var appointment = AddAppointment();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _send.Handle(
                new SendRoomChatMessageCommand(appointment.Id, "intruso", Guid.NewGuid(), false),
                CancellationToken.None));

        Assert.Empty(_messages.Items);
    }

    [Fact]
    public async Task Send_CitaInexistente_LanzaNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _send.Handle(
                new SendRoomChatMessageCommand(Guid.NewGuid(), "hola", TestData.UserId, true),
                CancellationToken.None));
    }

    [Fact]
    public async Task Send_CitaConfirmada_EsPermitida()
    {
        var appointment = AddAppointment(AppointmentStatus.Confirmed);
        AsProfessional(appointment);

        var dto = await _send.Handle(
            new SendRoomChatMessageCommand(appointment.Id, "antes", TestData.UserId, false),
            CancellationToken.None);

        Assert.Equal("antes", dto.Body);
        Assert.Single(_messages.Items);
    }

    [Fact]
    public async Task Send_CitaCompletada_EsPermitida()
    {
        var appointment = AddAppointment(AppointmentStatus.Completed);
        AsProfessional(appointment);

        var dto = await _send.Handle(
            new SendRoomChatMessageCommand(appointment.Id, "post-consulta", TestData.UserId, false),
            CancellationToken.None);

        Assert.Equal("post-consulta", dto.Body);
    }

    [Theory]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    [InlineData(AppointmentStatus.Requested)]
    public async Task Send_EstadosNoDisponibles_LanzaViolacion(AppointmentStatus status)
    {
        var appointment = AddAppointment(status);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _send.Handle(
                new SendRoomChatMessageCommand(appointment.Id, "hola", Guid.NewGuid(), true),
                CancellationToken.None));
    }

    [Fact]
    public async Task List_OrdenaYRespetaCursorAfterYLimite()
    {
        var appointment = AddAppointment();
        AsPatient(appointment);
        var t0 = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < 3; i++)
        {
            _messages.Items.Add(new ChatMessage
            {
                AppointmentId = appointment.Id,
                SenderUserId = TestData.PatientUserId,
                SenderRole = "Patient",
                Body = $"m{i}",
                CreatedAt = t0.AddSeconds(i).UtcDateTime,
            });
        }

        var all = await _list.Handle(
            new GetRoomChatMessagesQuery(appointment.Id, TestData.PatientUserId, false),
            CancellationToken.None);
        Assert.Equal(["m0", "m1", "m2"], all.Select(m => m.Body));

        var afterFirst = await _list.Handle(
            new GetRoomChatMessagesQuery(
                appointment.Id, TestData.PatientUserId, false,
                After: all[0].CreatedAt, AfterId: all[0].Id),
            CancellationToken.None);
        Assert.Equal(["m1", "m2"], afterFirst.Select(m => m.Body));

        var afterLast = await _list.Handle(
            new GetRoomChatMessagesQuery(
                appointment.Id, TestData.PatientUserId, false,
                After: all[2].CreatedAt, AfterId: all[2].Id),
            CancellationToken.None);
        Assert.Empty(afterLast);

        var limited = await _list.Handle(
            new GetRoomChatMessagesQuery(appointment.Id, TestData.PatientUserId, false, Limit: 2),
            CancellationToken.None);
        Assert.Equal(["m0", "m1"], limited.Select(m => m.Body));
    }

    [Fact]
    public async Task List_UsuarioAjeno_LanzaForbidden()
    {
        var appointment = AddAppointment();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _list.Handle(
                new GetRoomChatMessagesQuery(appointment.Id, Guid.NewGuid(), false),
                CancellationToken.None));
    }

    [Fact]
    public async Task List_CitaConfirmada_EsPermitida()
    {
        var appointment = AddAppointment(AppointmentStatus.Confirmed);
        AsProfessional(appointment);

        var result = await _list.Handle(
            new GetRoomChatMessagesQuery(appointment.Id, TestData.UserId, false),
            CancellationToken.None);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    [InlineData(AppointmentStatus.Requested)]
    public async Task List_EstadosNoDisponibles_LanzaViolacion(AppointmentStatus status)
    {
        var appointment = AddAppointment(status);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _list.Handle(
                new GetRoomChatMessagesQuery(appointment.Id, Guid.NewGuid(), true),
                CancellationToken.None));
    }

    [Fact]
    public async Task List_Supervisor_EsPermitido()
    {
        var appointment = AddAppointment();

        var result = await _list.Handle(
            new GetRoomChatMessagesQuery(appointment.Id, Guid.NewGuid(), true),
            CancellationToken.None);

        Assert.Empty(result);
    }
}
