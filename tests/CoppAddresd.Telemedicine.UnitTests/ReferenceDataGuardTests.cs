using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Domain.Exceptions;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Validación de datos de referencia del ERP (ReferenceDataGuard): existencia
/// de profesional/paciente/especialidad/sede antes de agendar.
/// </summary>
public class ReferenceDataGuardTests
{
    private readonly FakeReferenceDataService _referenceData = new();

    [Fact]
    public async Task RequireProfessional_Existente_Devuelve()
    {
        _referenceData.Professionals[TestData.ProfessionalId] = TestData.Professional();

        var result = await ReferenceDataGuard.RequireProfessionalAsync(
            _referenceData, TestData.ProfessionalId, CancellationToken.None);

        Assert.Equal(TestData.ProfessionalId, result.Id);
    }

    [Fact]
    public async Task RequireProfessional_Inexistente_LanzaNotFound()
        => await Assert.ThrowsAsync<NotFoundException>(() =>
            ReferenceDataGuard.RequireProfessionalAsync(_referenceData, Guid.NewGuid(), CancellationToken.None));

    [Fact]
    public async Task RequirePatient_Inexistente_LanzaNotFound()
        => await Assert.ThrowsAsync<NotFoundException>(() =>
            ReferenceDataGuard.RequirePatientAsync(_referenceData, Guid.NewGuid(), CancellationToken.None));

    [Fact]
    public async Task RequireSpecialty_Inexistente_LanzaNotFound()
        => await Assert.ThrowsAsync<NotFoundException>(() =>
            ReferenceDataGuard.RequireSpecialtyAsync(_referenceData, Guid.NewGuid(), CancellationToken.None));

    [Fact]
    public async Task RequireLocation_Null_DevuelveNullSinConsultar()
    {
        var result = await ReferenceDataGuard.RequireLocationAsync(_referenceData, null, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task RequireLocation_Inexistente_LanzaNotFound()
        => await Assert.ThrowsAsync<NotFoundException>(() =>
            ReferenceDataGuard.RequireLocationAsync(_referenceData, Guid.NewGuid(), CancellationToken.None));
}
