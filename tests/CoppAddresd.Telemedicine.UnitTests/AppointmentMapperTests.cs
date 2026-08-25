using CoppAddresd.Telemedicine.Application.Features.Telemedicine;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Constructor de DTOs de cita (AppointmentMapper.BuildDtosAsync):
/// enriquece con nombres del ERP deduplicando los fetch por entidad única.
/// </summary>
public class AppointmentMapperTests
{
    private readonly FakeReferenceDataService _referenceData = new();

    [Fact]
    public async Task BuildDtosAsync_EnriqueceNombres()
    {
        var appointment = TestData.Appointment();
        _referenceData.Professionals[appointment.ProfessionalId] = TestData.Professional(userId: TestData.UserId);
        _referenceData.Patients[appointment.PatientId] = TestData.Patient();
        _referenceData.Specialties[appointment.SpecialtyId] = TestData.Specialty();
        _referenceData.Locations[TestData.LocationId] = TestData.Location();

        var dtos = await AppointmentMapper.BuildDtosAsync(
            [appointment], _referenceData, CancellationToken.None);

        var dto = Assert.Single(dtos);
        Assert.Equal("Dra. Ana Pérez", dto.ProfessionalName);
        Assert.Equal("María Gómez", dto.PatientName);
        Assert.Equal("Medicina General", dto.SpecialtyName);
        Assert.Equal("Sede Principal", dto.LocationName);
        Assert.Equal(appointment.Status, dto.Status);
    }

    [Fact]
    public async Task BuildDtosAsync_ReferenciaAusente_NombreNullSinRomper()
    {
        var appointment = TestData.Appointment();

        var dtos = await AppointmentMapper.BuildDtosAsync(
            [appointment], _referenceData, CancellationToken.None);

        var dto = Assert.Single(dtos);
        Assert.Null(dto.ProfessionalName);
        Assert.Null(dto.PatientName);
        Assert.Null(dto.SpecialtyName);
        Assert.Null(dto.LocationName);
        Assert.Equal(appointment.Id, dto.Id);
    }

    [Fact]
    public async Task BuildDtosAsync_SinSede_LocationNameNull()
    {
        var appointment = TestData.Appointment(locationId: null);

        var dtos = await AppointmentMapper.BuildDtosAsync(
            [appointment], _referenceData, CancellationToken.None);

        Assert.Null(Assert.Single(dtos).LocationName);
    }

    [Fact]
    public async Task BuildDtosAsync_DedupReferencia_CadaEntidadSeConsultaUnaVez()
    {
        var a1 = TestData.Appointment(patientId: TestData.PatientId);
        var a2 = TestData.Appointment(patientId: TestData.PatientId);
        _referenceData.Patients[TestData.PatientId] = TestData.Patient();
        _referenceData.Professionals[TestData.ProfessionalId] = TestData.Professional(userId: TestData.UserId);
        _referenceData.Specialties[TestData.SpecialtyId] = TestData.Specialty();

        var dtos = await AppointmentMapper.BuildDtosAsync(
            [a1, a2], _referenceData, CancellationToken.None);

        Assert.Equal(2, dtos.Count);
        Assert.All(dtos, d => Assert.Equal("María Gómez", d.PatientName));
    }
}
