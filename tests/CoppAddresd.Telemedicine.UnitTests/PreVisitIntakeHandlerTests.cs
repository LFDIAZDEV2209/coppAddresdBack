using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Pre-consulta del paciente (F4): matriz de autorización (el paciente escribe
/// y lee la propia; el profesional/supervisor solo leen; ajeno 403), ciclo de
/// vida (editable solo con la cita <c>Confirmed</c>; después 409 en escritura y
/// lectura disponible), upsert sin duplicar fila, identidad derivada del JWT y
/// validaciones de longitud.
/// </summary>
public class PreVisitIntakeHandlerTests
{
    private readonly FakeReferenceDataService _referenceData = new();
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakePreVisitIntakeRepository _intakes = new();
    private readonly UpsertPreVisitIntakeCommandHandler _upsert;
    private readonly GetPreVisitIntakeQueryHandler _get;

    public PreVisitIntakeHandlerTests()
    {
        _upsert = new UpsertPreVisitIntakeCommandHandler(
            _appointments, _intakes, _referenceData,
            NullLogger<UpsertPreVisitIntakeCommandHandler>.Instance);
        _get = new GetPreVisitIntakeQueryHandler(_appointments, _intakes, _referenceData);
    }

    private Domain.Entities.Appointment AddAppointment(
        AppointmentStatus status = AppointmentStatus.Confirmed)
    {
        var appointment = TestData.Appointment(status: status);
        _appointments.Items.Add(appointment);
        return appointment;
    }

    private void AsProfessional(Domain.Entities.Appointment appointment)
    {
        _referenceData.Professionals[appointment.ProfessionalId] =
            TestData.Professional(userId: TestData.UserId);
        _referenceData.UserToProfessional[TestData.UserId] = appointment.ProfessionalId;
    }

    private void AsPatient(Domain.Entities.Appointment appointment)
    {
        _referenceData.Patients[appointment.PatientId] = TestData.Patient();
        _referenceData.UserToPatient[TestData.PatientUserId] = appointment.PatientId;
    }

    private static UpsertPreVisitIntakeCommand Command(
        Domain.Entities.Appointment appointment,
        Guid? userId = null,
        bool hasManagePermission = false,
        string reason = "Dolor de cabeza",
        string? symptoms = null,
        string? allergies = null,
        string? medications = null) =>
        new(
            appointment.Id,
            reason,
            symptoms,
            allergies,
            medications,
            userId ?? TestData.PatientUserId,
            hasManagePermission);

    [Fact]
    public async Task Upsert_Paciente_CreaIdentidadDerivadaYTrimeaCampos()
    {
        var appointment = AddAppointment();
        AsPatient(appointment);

        var dto = await _upsert.Handle(
            Command(
                appointment,
                reason: "  Dolor cervical  ",
                symptoms: " rigidez ",
                allergies: "",
                medications: "  ibuprofeno  "),
            CancellationToken.None);

        Assert.Single(_intakes.Items);
        var intake = _intakes.Items[0];
        Assert.Equal(appointment.Id, intake.AppointmentId);
        Assert.Equal(appointment.PatientId, intake.PatientId);
        Assert.Equal(TestData.PatientUserId, intake.CreatedBy);
        Assert.Equal("Dolor cervical", intake.Reason);
        Assert.Equal("rigidez", intake.Symptoms);
        Assert.Null(intake.Allergies);
        Assert.Equal("ibuprofeno", intake.Medications);
        Assert.Null(intake.UpdatedAt);
        Assert.Equal(dto.Id, intake.Id);
        Assert.Null(dto.UpdatedAt);
    }

    [Fact]
    public async Task Upsert_PacienteActualiza_NoDuplicaYSetUpdatedAt()
    {
        var appointment = AddAppointment();
        AsPatient(appointment);

        var first = await _upsert.Handle(Command(appointment), CancellationToken.None);
        var second = await _upsert.Handle(
            Command(appointment, reason: "Dolor lumbar", symptoms: "fiebre"),
            CancellationToken.None);

        Assert.Single(_intakes.Items);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal("Dolor lumbar", _intakes.Items[0].Reason);
        Assert.Equal("fiebre", _intakes.Items[0].Symptoms);
        Assert.NotNull(_intakes.Items[0].UpdatedAt);
        Assert.NotNull(second.UpdatedAt);
    }

    [Fact]
    public async Task Upsert_Profesional_LanzaForbidden()
    {
        var appointment = AddAppointment();
        AsProfessional(appointment);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _upsert.Handle(
                Command(appointment, userId: TestData.UserId),
                CancellationToken.None));

        Assert.Empty(_intakes.Items);
    }

    [Fact]
    public async Task Upsert_Supervisor_LanzaForbidden()
    {
        var appointment = AddAppointment();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _upsert.Handle(
                Command(appointment, userId: Guid.NewGuid(), hasManagePermission: true),
                CancellationToken.None));

        Assert.Empty(_intakes.Items);
    }

    [Fact]
    public async Task Upsert_UsuarioAjeno_LanzaForbidden()
    {
        var appointment = AddAppointment();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _upsert.Handle(
                Command(appointment, userId: Guid.NewGuid()),
                CancellationToken.None));

        Assert.Empty(_intakes.Items);
    }

    [Fact]
    public async Task Upsert_CitaInexistente_LanzaNotFound()
    {
        var appointment = TestData.Appointment();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _upsert.Handle(
                Command(appointment),
                CancellationToken.None));
    }

    [Theory]
    [InlineData(AppointmentStatus.Requested)]
    [InlineData(AppointmentStatus.InProgress)]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public async Task Upsert_EstadoNoEditable_LanzaViolacion(AppointmentStatus status)
    {
        var appointment = AddAppointment(status);
        AsPatient(appointment);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _upsert.Handle(Command(appointment), CancellationToken.None));

        Assert.Empty(_intakes.Items);
    }

    [Fact]
    public async Task Upsert_AutoguardadoConcurrente_ActualizaLaExistente()
    {
        var appointment = AddAppointment();
        AsPatient(appointment);

        // Simula la carrera: la fila ya existe al llegar el segundo guardado.
        _intakes.Items.Add(new Domain.Entities.PreVisitIntake
        {
            AppointmentId = appointment.Id,
            PatientId = appointment.PatientId,
            Reason = "previo",
            CreatedBy = TestData.PatientUserId,
            CreatedAt = DateTime.UtcNow.AddMinutes(-1),
        });

        var dto = await _upsert.Handle(
            Command(appointment, reason: "nuevo motivo"),
            CancellationToken.None);

        Assert.Single(_intakes.Items);
        Assert.Equal("nuevo motivo", dto.Reason);
    }

    [Fact]
    public async Task Get_PacienteSinRegistro_DevuelveNull()
    {
        var appointment = AddAppointment();
        AsPatient(appointment);

        var dto = await _get.Handle(
            new GetPreVisitIntakeQuery(appointment.Id, TestData.PatientUserId, false),
            CancellationToken.None);

        Assert.Null(dto);
    }

    [Fact]
    public async Task Get_PacienteProfesionalYSupervisor_LeenElRegistro()
    {
        var appointment = AddAppointment();
        AsPatient(appointment);
        AsProfessional(appointment);
        await _upsert.Handle(
            Command(appointment, reason: "Cefalea", allergies: "penicilina"),
            CancellationToken.None);

        var asPatient = await _get.Handle(
            new GetPreVisitIntakeQuery(appointment.Id, TestData.PatientUserId, false),
            CancellationToken.None);
        var asProfessional = await _get.Handle(
            new GetPreVisitIntakeQuery(appointment.Id, TestData.UserId, false),
            CancellationToken.None);
        var asSupervisor = await _get.Handle(
            new GetPreVisitIntakeQuery(appointment.Id, Guid.NewGuid(), true),
            CancellationToken.None);

        Assert.Equal("Cefalea", asPatient?.Reason);
        Assert.Equal("Cefalea", asProfessional?.Reason);
        Assert.Equal("penicilina", asSupervisor?.Allergies);
    }

    [Fact]
    public async Task Get_CitaEnCurso_SigueDisponibleEnLectura()
    {
        var appointment = AddAppointment();
        AsPatient(appointment);
        await _upsert.Handle(Command(appointment), CancellationToken.None);

        appointment.Status = AppointmentStatus.InProgress;

        var dto = await _get.Handle(
            new GetPreVisitIntakeQuery(appointment.Id, TestData.UserId, true),
            CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal("Dolor de cabeza", dto!.Reason);
    }

    [Fact]
    public async Task Get_UsuarioAjeno_LanzaForbidden()
    {
        var appointment = AddAppointment();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _get.Handle(
                new GetPreVisitIntakeQuery(appointment.Id, Guid.NewGuid(), false),
                CancellationToken.None));
    }

    [Fact]
    public void Validator_MotivoVacioOExcedido_Invalido()
    {
        var validator = new UpsertPreVisitIntakeCommandValidator();

        Assert.False(validator.Validate(Command(TestData.Appointment(), reason: "   ")).IsValid);
        Assert.False(validator.Validate(
            Command(TestData.Appointment(), reason: new string('x', 501))).IsValid);
    }

    [Fact]
    public void Validator_TextosOpcionalesExcedidos_Invalido()
    {
        var validator = new UpsertPreVisitIntakeCommandValidator();

        Assert.False(validator.Validate(
            Command(TestData.Appointment(), symptoms: new string('x', 4001))).IsValid);
        Assert.False(validator.Validate(
            Command(TestData.Appointment(), allergies: new string('x', 2001))).IsValid);
        Assert.False(validator.Validate(
            Command(TestData.Appointment(), medications: new string('x', 2001))).IsValid);
    }

    [Fact]
    public void Validator_Valido_EsValido()
    {
        var validator = new UpsertPreVisitIntakeCommandValidator();

        var result = validator.Validate(Command(
            TestData.Appointment(),
            reason: "Dolor abdominal",
            symptoms: "náuseas",
            allergies: "ninguna",
            medications: "omeprazol"));

        Assert.True(result.IsValid);
    }
}
