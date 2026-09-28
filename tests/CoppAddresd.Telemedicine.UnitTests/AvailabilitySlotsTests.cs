using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Application.ReferenceData;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using FluentValidation;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Disponibilidad de ranuras (<see cref="GetAvailabilitySlotsQueryHandler"/>) y
/// reprogramación directa por paciente (Decisión 2, Opción A): modos profesional
/// y especialidad, anticipación mínima, solapamiento, UTC y propiedad.
/// </summary>
public class AvailabilitySlotsTests
{
    private readonly FakeReferenceDataService _referenceData = new();
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeSettingsProvider _settings = new();
    private readonly GetAvailabilitySlotsQueryHandler _handler;

    public AvailabilitySlotsTests()
    {
        _handler = new GetAvailabilitySlotsQueryHandler(_appointments, _referenceData, _settings);
        _referenceData.Professionals[TestData.ProfessionalId] = TestData.Professional(
            userId: TestData.UserId
        );
        _referenceData.Specialties[TestData.SpecialtyId] = TestData.Specialty();
    }

    private static DateOnly NextWeekday(DayOfWeek day)
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        while (date.DayOfWeek != day)
        {
            date = date.AddDays(1);
        }
        return date;
    }

    private static int IsoWeekday(DateOnly date) => ((int)date.DayOfWeek + 6) % 7 + 1;

    private static DateTimeOffset UtcOf(DateOnly date, int hour, int minute = 0) =>
        new(date.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Utc));

    [Fact]
    public async Task Handle_ProfesionalSinHorario_RetornaVacia()
    {
        var date = NextWeekday(DayOfWeek.Monday);
        var query = new GetAvailabilitySlotsQuery(
            TestData.ProfessionalId,
            null,
            TestData.Org,
            TestData.Clinic,
            null,
            date
        );

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(TestData.ProfessionalId, result.ProfessionalId);
        Assert.Null(result.SpecialtyId);
        Assert.Empty(result.Slots);
    }

    [Fact]
    public async Task Handle_ProfesionalInexistente_LanzaNotFound()
    {
        var query = new GetAvailabilitySlotsQuery(
            Guid.NewGuid(),
            null,
            TestData.Org,
            null,
            null,
            NextWeekday(DayOfWeek.Monday)
        );

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(query, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Handle_RanuraSolapada_MarcaBookedYConservaLibres()
    {
        var date = NextWeekday(DayOfWeek.Monday);
        _referenceData.Schedules[TestData.ProfessionalId] =
        [
            new ProfessionalScheduleRefDto(IsoWeekday(date), "09:00", "10:00"),
        ];
        _appointments.Items.Add(
            TestData.Appointment(professionalId: TestData.ProfessionalId, start: UtcOf(date, 9, 30))
        );
        var query = new GetAvailabilitySlotsQuery(
            TestData.ProfessionalId,
            null,
            TestData.Org,
            null,
            null,
            date
        );

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(2, result.Slots.Count);
        var first = result.Slots[0];
        Assert.True(first.IsAvailable);
        Assert.Null(first.ConflictReason);
        Assert.Equal(1, first.AvailableProfessionalCount);
        var second = result.Slots[1];
        Assert.False(second.IsAvailable);
        Assert.Equal("Booked", second.ConflictReason);
        Assert.Equal(0, second.AvailableProfessionalCount);
        Assert.Equal("+00:00", result.TimezoneOffset);
    }

    [Fact]
    public async Task Handle_AnticipacionMinima_MarcaTooSoon()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        _referenceData.Schedules[TestData.ProfessionalId] =
        [
            new ProfessionalScheduleRefDto(IsoWeekday(date), "00:00", "23:59"),
        ];
        var query = new GetAvailabilitySlotsQuery(
            TestData.ProfessionalId,
            null,
            TestData.Org,
            null,
            null,
            date
        );

        var result = await _handler.Handle(query, CancellationToken.None);

        var now = DateTimeOffset.UtcNow;
        Assert.NotEmpty(result.Slots);
        foreach (var slot in result.Slots)
        {
            if (slot.Start < now.AddHours(_settings.Settings.MinAdvanceBookingHours))
            {
                Assert.False(slot.IsAvailable);
                Assert.Equal("TooSoon", slot.ConflictReason);
            }
            else
            {
                Assert.True(slot.IsAvailable);
            }
        }
        Assert.Contains(result.Slots, s => s.ConflictReason == "TooSoon");
    }

    [Fact]
    public async Task Handle_CitaConOffsetLocal_SolapaEnUtc()
    {
        var date = NextWeekday(DayOfWeek.Monday);
        _referenceData.Schedules[TestData.ProfessionalId] =
        [
            new ProfessionalScheduleRefDto(IsoWeekday(date), "09:00", "10:00"),
        ];
        // 09:00 UTC expresado con offset +05:00: el instante es el mismo.
        var startConOffset = new DateTimeOffset(
            date.ToDateTime(new TimeOnly(14, 0)),
            TimeSpan.FromHours(5)
        );
        _appointments.Items.Add(
            TestData.Appointment(professionalId: TestData.ProfessionalId, start: startConOffset)
        );
        var query = new GetAvailabilitySlotsQuery(
            TestData.ProfessionalId,
            null,
            TestData.Org,
            null,
            null,
            date
        );

        var result = await _handler.Handle(query, CancellationToken.None);

        var slot = Assert.Single(result.Slots, s => s.Start == UtcOf(date, 9));
        Assert.False(slot.IsAvailable);
        Assert.Equal("Booked", slot.ConflictReason);
        Assert.Equal(UtcOf(date, 9), slot.Start);
        Assert.Equal(TimeSpan.Zero, slot.Start.Offset);
    }

    [Fact]
    public async Task Handle_ModoEspecialidad_UneSlotsYCuentaProfesionales()
    {
        var date = NextWeekday(DayOfWeek.Tuesday);
        var weekday = IsoWeekday(date);
        var otherProfessional = Guid.NewGuid();
        _referenceData.Candidates =
        [
            new ProfessionalCandidateRefDto(
                TestData.ProfessionalId,
                Guid.NewGuid(),
                TestData.UserId,
                "Dra. Ana Pérez",
                [TestData.Clinic],
                [TestData.LocationId],
                [new ProfessionalScheduleRefDto(weekday, "09:00", "10:00")]
            ),
            new ProfessionalCandidateRefDto(
                otherProfessional,
                Guid.NewGuid(),
                null,
                "Dr. Luis Gómez",
                [TestData.Clinic],
                [TestData.LocationId],
                [new ProfessionalScheduleRefDto(weekday, "09:00", "10:00")]
            ),
        ];
        _appointments.Items.Add(
            TestData.Appointment(professionalId: TestData.ProfessionalId, start: UtcOf(date, 9))
        );
        var query = new GetAvailabilitySlotsQuery(
            null,
            TestData.SpecialtyId,
            TestData.Org,
            TestData.Clinic,
            TestData.LocationId,
            date
        );

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Null(result.ProfessionalId);
        Assert.Equal(TestData.SpecialtyId, result.SpecialtyId);
        Assert.Equal(2, result.Slots.Count);
        var first = result.Slots[0];
        Assert.True(first.IsAvailable);
        Assert.Equal(1, first.AvailableProfessionalCount);
        var second = result.Slots[1];
        Assert.True(second.IsAvailable);
        Assert.Equal(2, second.AvailableProfessionalCount);
    }

    [Fact]
    public async Task Handle_ModoEspecialidadSinCandidatos_RetornaVacia()
    {
        _referenceData.Candidates = [];
        var query = new GetAvailabilitySlotsQuery(
            null,
            TestData.SpecialtyId,
            TestData.Org,
            null,
            null,
            NextWeekday(DayOfWeek.Wednesday)
        );

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Empty(result.Slots);
    }

    [Fact]
    public async Task Handle_ModoEspecialidadInexistente_LanzaNotFound()
    {
        _referenceData.Candidates = null;
        var query = new GetAvailabilitySlotsQuery(
            null,
            Guid.NewGuid(),
            TestData.Org,
            null,
            null,
            NextWeekday(DayOfWeek.Wednesday)
        );

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(query, CancellationToken.None)
        );
    }

    [Fact]
    public void Validate_SinProfesionalNiEspecialidad_Invalido()
    {
        var validator = new GetAvailabilitySlotsQueryValidator();
        var query = new GetAvailabilitySlotsQuery(
            null,
            null,
            TestData.Org,
            null,
            null,
            NextWeekday(DayOfWeek.Monday)
        );

        Assert.False(validator.Validate(query).IsValid);
    }

    [Fact]
    public void Validate_EspecialidadSinOrganizacion_Invalido()
    {
        var validator = new GetAvailabilitySlotsQueryValidator();
        var query = new GetAvailabilitySlotsQuery(
            null,
            TestData.SpecialtyId,
            null,
            null,
            null,
            NextWeekday(DayOfWeek.Monday)
        );

        Assert.False(validator.Validate(query).IsValid);
    }

    [Fact]
    public void Validate_FechaDefault_Invalido()
    {
        var validator = new GetAvailabilitySlotsQueryValidator();
        var query = new GetAvailabilitySlotsQuery(
            TestData.ProfessionalId,
            null,
            null,
            null,
            null,
            default
        );

        Assert.False(validator.Validate(query).IsValid);
    }

    [Fact]
    public void Validate_ModoProfesionalSinOrganizacion_Valido()
    {
        var validator = new GetAvailabilitySlotsQueryValidator();
        var query = new GetAvailabilitySlotsQuery(
            TestData.ProfessionalId,
            null,
            null,
            null,
            null,
            NextWeekday(DayOfWeek.Monday)
        );

        Assert.True(validator.Validate(query).IsValid);
    }
}

/// <summary>
/// Reprogramación directa por paciente (Decisión 2, Opción A): propiedad vía
/// perfil resuelto por usuario Auth, forzado de <c>RequestedBy.Patient</c> y
/// ventana de anticipación de la cita original.
/// </summary>
public class PatientRescheduleHandlerTests
{
    private readonly FakeReferenceDataService _referenceData = new();
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeSettingsProvider _settings = new();
    private readonly FakeAlertRepository _alerts = new();
    private readonly RescheduleAppointmentCommandHandler _handler;

    private readonly Guid _otherUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _otherPatientId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    public PatientRescheduleHandlerTests()
    {
        _handler = new RescheduleAppointmentCommandHandler(
            _appointments,
            _referenceData,
            _settings,
            _alerts
        );
        _referenceData.Professionals[TestData.ProfessionalId] = TestData.Professional(
            userId: TestData.UserId
        );
        _referenceData.Patients[TestData.PatientId] = TestData.Patient(
            userId: TestData.PatientUserId
        );
        _referenceData.UserToPatient[TestData.PatientUserId] = TestData.PatientId;
        _referenceData.Patients[_otherPatientId] = TestData.Patient(
            id: _otherPatientId,
            userId: _otherUserId
        );
        _referenceData.UserToPatient[_otherUserId] = _otherPatientId;
        _referenceData.Specialties[TestData.SpecialtyId] = TestData.Specialty();
    }

    [Fact]
    public async Task Handle_PacienteDueno_ReprogramaYFuerzaRequestedByPatient()
    {
        var appointment = TestData.Appointment(start: DateTimeOffset.UtcNow.AddDays(3));
        _appointments.Items.Add(appointment);
        var command = new RescheduleAppointmentCommand(
            appointment.Id,
            DateTimeOffset.UtcNow.AddDays(4),
            null,
            "Cambio de horario",
            RescheduleRequestedBy.Admin,
            TestData.PatientUserId,
            TestData.PatientUserId
        );

        var dto = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(1, dto.RescheduleCount);
        var reschedule = Assert.Single(appointment.Reschedules);
        Assert.Equal(RescheduleRequestedBy.Patient, reschedule.RequestedBy);
    }

    [Fact]
    public async Task Handle_PacienteAjeno_LanzaForbidden()
    {
        var appointment = TestData.Appointment(start: DateTimeOffset.UtcNow.AddDays(3));
        _appointments.Items.Add(appointment);
        var command = new RescheduleAppointmentCommand(
            appointment.Id,
            DateTimeOffset.UtcNow.AddDays(4),
            null,
            null,
            RescheduleRequestedBy.Patient,
            _otherUserId,
            _otherUserId
        );

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _handler.Handle(command, CancellationToken.None)
        );
        Assert.Empty(appointment.Reschedules);
    }

    [Fact]
    public async Task Handle_PacienteSinPerfil_LanzaForbidden()
    {
        var appointment = TestData.Appointment(start: DateTimeOffset.UtcNow.AddDays(3));
        _appointments.Items.Add(appointment);
        var command = new RescheduleAppointmentCommand(
            appointment.Id,
            DateTimeOffset.UtcNow.AddDays(4),
            null,
            null,
            RescheduleRequestedBy.Patient,
            Guid.NewGuid(),
            Guid.NewGuid()
        );

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _handler.Handle(command, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Handle_CitaEnVentanaDeAnticipacion_LanzaViolacion()
    {
        var appointment = TestData.Appointment(start: DateTimeOffset.UtcNow.AddHours(1));
        _appointments.Items.Add(appointment);
        var command = new RescheduleAppointmentCommand(
            appointment.Id,
            DateTimeOffset.UtcNow.AddDays(2),
            null,
            null,
            RescheduleRequestedBy.Admin,
            TestData.UserId
        );

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _handler.Handle(command, CancellationToken.None)
        );
        Assert.Empty(appointment.Reschedules);
    }

    [Fact]
    public async Task Handle_ErpSinPaciente_ConservaRequestedByDelBody()
    {
        var appointment = TestData.Appointment(start: DateTimeOffset.UtcNow.AddDays(3));
        _appointments.Items.Add(appointment);
        var command = new RescheduleAppointmentCommand(
            appointment.Id,
            DateTimeOffset.UtcNow.AddDays(4),
            null,
            null,
            RescheduleRequestedBy.Professional,
            TestData.UserId
        );

        await _handler.Handle(command, CancellationToken.None);

        var reschedule = Assert.Single(appointment.Reschedules);
        Assert.Equal(RescheduleRequestedBy.Professional, reschedule.RequestedBy);
    }
}
