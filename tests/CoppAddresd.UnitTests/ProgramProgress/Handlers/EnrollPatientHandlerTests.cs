using CoppAddresd.Application.Features.ProgramProgress.Commands.EnrollPatient;
using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.UnitTests.Cache;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.UnitTests.ProgramProgress.Handlers;

/// <summary>
/// Tests del caso de uso de inscripción de paciente (SPEC §7.5): resolución de
/// la plantilla por defecto (Program:DefaultTemplate:Code → fallback
/// default-83w), default de startLocalDate al lunes local, devolución del DTO
/// de inscripción con su estado de gamificación e invalidación del caché de
/// scores-history tras el commit (re-inscripción = historia nueva, SPEC
/// §13.7.3).
/// </summary>
public class EnrollPatientHandlerTests
{
    private readonly FakeProgramRepository _repository = new();
    private readonly FakeCacheService _cache = new();
    private readonly EnrollPatientCommandHandler _handler;
    private readonly Guid _templateId = Guid.NewGuid();

    public EnrollPatientHandlerTests()
    {
        _handler = new EnrollPatientCommandHandler(
            _repository, _cache, NullLogger<EnrollPatientCommandHandler>.Instance);

        _repository.Templates[_templateId] = new ProgramTemplate
        {
            Id = _templateId,
            Code = "default-83w",
            Name = "Programa 83 semanas",
            TotalWeeks = 83,
            Status = TemplateStatus.Active,
            Version = 1,
        };
    }

    [Fact]
    public async Task Handle_ConPlantillaExplicita_DevuelveDtoInscripcion()
    {
        var patientId = Guid.NewGuid();
        var command = new EnrollPatientCommand(
            patientId, _templateId, "America/Bogota", new DateOnly(2026, 9, 21));

        var dto = await _handler.Handle(command, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(patientId, dto.PatientId);
        Assert.Equal(_templateId, dto.TemplateId);
        Assert.Equal("America/Bogota", dto.Timezone);
        Assert.Equal(ProgramEnrollmentStatus.Active, dto.Status);
        Assert.Equal(new DateOnly(2026, 9, 21), dto.StartLocalDate);
        Assert.Equal(1, dto.CurrentWeekNumber);
        var enrollment = Assert.Single(_repository.Enrollments.Values);
        Assert.Equal(patientId, enrollment.PatientId);
    }

    [Fact]
    public async Task Handle_SinPlantilla_ResuelvePorDefectoPorCodigo()
    {
        // Resuelve la plantilla por defecto por código (fallback default-83w).
        var command = new EnrollPatientCommand(
            Guid.NewGuid(), TemplateId: null, "America/Bogota", new DateOnly(2026, 9, 21));

        var dto = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(_templateId, dto.TemplateId);
    }

    [Fact]
    public async Task Handle_SinPlantillaConCodigoConfigurado_UsaCodigo()
    {
        // La capa API lee Program:DefaultTemplate:Code y lo pasa al comando.
        var other = Guid.NewGuid();
        _repository.Templates[other] = new ProgramTemplate
        {
            Id = other,
            Code = "custom-code",
            Name = "Plantilla custom",
            TotalWeeks = 12,
            Status = TemplateStatus.Active,
            Version = 1,
        };

        var command = new EnrollPatientCommand(
            Guid.NewGuid(), TemplateId: null, "America/Bogota",
            new DateOnly(2026, 9, 21), DefaultTemplateCode: "custom-code");

        var dto = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(other, dto.TemplateId);
    }

    [Fact]
    public async Task Handle_SinPlantillaPorDefecto_LanzaNotFound()
    {
        // Plantilla por defecto inexistente → 404 (no se fabrica una).
        _repository.Templates.Clear();
        var command = new EnrollPatientCommand(
            Guid.NewGuid(), TemplateId: null, "America/Bogota", new DateOnly(2026, 9, 21));

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("TEMPLATE_NOT_FOUND", ex.Message);
        Assert.Empty(_repository.Enrollments);
    }

    [Fact]
    public async Task Handle_SinFechaInicio_UsaLunesDeSemanaLocal()
    {
        // Default de startLocalDate: el lunes de la semana local actual del
        // paciente (SPEC §6.11). Se computa el esperado con el mismo reloj que
        // el handler (hoy real en la zona del paciente → lunes).
        var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));
        var expectedMonday = localToday.AddDays(-(((int)localToday.DayOfWeek + 6) % 7));

        var command = new EnrollPatientCommand(
            Guid.NewGuid(), _templateId, "America/Bogota", StartLocalDate: null);

        var dto = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(expectedMonday, dto.StartLocalDate);
    }

    [Fact]
    public async Task Handle_ZonaInvalida_LanzaUnprocessable()
    {
        // Defensa en profundidad: aunque el pipeline valide, el handler no
        // persiste con una zona IANA inválida.
        var command = new EnrollPatientCommand(
            Guid.NewGuid(), _templateId, "Mars/Olympus", new DateOnly(2026, 9, 21));

        var ex = await Assert.ThrowsAsync<UnprocessableEntityException>(
            () => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("INVALID_TIMEZONE", ex.Message);
        Assert.Empty(_repository.Enrollments);
    }

    /// <summary>
    /// Re-inscripción (tras retiro/completación): el handler invalida el caché
    /// de scores-history del paciente post-commit (la serie cacheada de la
    /// corrida anterior no debe servirse a la nueva — historia por programa,
    /// SPEC §13.7.3). La purga de las filas en BD es responsabilidad de
    /// EnrollAsync (integration-only, ProgramRepositoryTests).
    /// </summary>
    [Fact]
    public async Task Handle_ReInscripcion_InvalidaElCacheDeScoresHistory()
    {
        var patientId = Guid.NewGuid();
        var command = new EnrollPatientCommand(
            patientId, _templateId, "America/Bogota", new DateOnly(2026, 9, 21));

        await _handler.Handle(command, CancellationToken.None);

        Assert.Equal([$"scores-history:{patientId}:v1"], _cache.Removed);
    }
}