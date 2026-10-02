using CoppAddresd.Application.Features.Patients;

namespace CoppAddresd.UnitTests.Features.Patients;

/// <summary>
/// Validación de creación de pacientes: el registro provisional del ERP
/// (Pendiente) es válido, los campos de identidad son obligatorios en alta y
/// los errores de vocabulario llevan el nombre real de la propiedad (antes
/// llegaban con PropertyName vacío y el ERP no podía mapearlos al campo).
/// </summary>
public class CreatePatientValidatorTests
{
    private static readonly DateTime BirthDate = new(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static CreatePatientCommand Valid(
        string? status = "Pendiente",
        EmergencyContactDto? emergencyContact = null,
        string? maritalStatus = null
    ) => new(
        MedicalRecordNumber: null,
        FirstName: "Mauricio",
        MiddleName: null,
        LastName: "Polo",
        DocumentTypeId: null,
        DocumentNumber: "12345678",
        DateOfBirth: BirthDate,
        Gender: "Femenino",
        EthnicityId: null,
        BloodTypeId: null,
        PhoneCountryCode: "1",
        PhoneNumber: "5551234567",
        Email: null,
        Address: null,
        CityId: null,
        StateId: null,
        CountryId: null,
        PostalCode: null,
        EmergencyContact: emergencyContact,
        InsurerId: null,
        MemberId: null,
        MaritalStatus: maritalStatus,
        SmokingStatus: null,
        AlcoholStatus: null,
        ExerciseLevel: null,
        Disability: null,
        HospitalizationHistory: null,
        SurgeryHistory: null,
        Status: status,
        Notes: null,
        ClinicId: null,
        LocationId: null,
        CreatedBy: null,
        CreatedByProfessionalId: null,
        Diagnoses: null,
        Medications: null,
        Allergies: null,
        VitalSigns: null
    );

    [Theory]
    [InlineData("Activo")]
    [InlineData("Inactivo")]
    [InlineData("Pendiente")]
    public void Validator_AceptaEstadosDelDirectorio(string status)
    {
        var result = new CreatePatientCommandValidator().Validate(Valid(status: status));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validator_RechazaEstadoDesconocidoConPropiedadReal()
    {
        var result = new CreatePatientCommandValidator().Validate(Valid(status: "Archivado"));

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors, e => e.PropertyName == "Status");
        Assert.Equal("El estado del paciente no es un valor válido.", error.ErrorMessage);
        Assert.DoesNotContain(result.Errors, e => e.PropertyName == string.Empty);
    }

    [Fact]
    public void Validator_ErroresDeVocabularioLlevanNombreDePropiedad()
    {
        var result = new CreatePatientCommandValidator().Validate(Valid(maritalStatus: "Casado"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "MaritalStatus");
    }

    [Fact]
    public void Validator_ExigeCamposDeIdentidadEnAlta()
    {
        AssertMissing(c => c with { DocumentNumber = null }, "DocumentNumber");
        AssertMissing(c => c with { DateOfBirth = null }, "DateOfBirth");
        AssertMissing(c => c with { Gender = null }, "Gender");
        AssertMissing(c => c with { PhoneNumber = null }, "PhoneNumber");
    }

    [Fact]
    public void Validator_ContactoDeEmergenciaConNombreSinTelefono_EsInvalido()
    {
        var command = Valid(emergencyContact: new EmergencyContactDto("Ana", "Madre", null, null));

        var result = new CreatePatientCommandValidator().Validate(command);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors, e => e.PropertyName == "EmergencyContact.Phone");
        Assert.Equal("El contacto de emergencia requiere teléfono.", error.ErrorMessage);
    }

    [Fact]
    public void Validator_ContactoDeEmergenciaCompleto_EsValido()
    {
        var command = Valid(
            emergencyContact: new EmergencyContactDto(
                "Ana",
                "Madre",
                "5551234567",
                "ana@example.com"
            )
        );

        Assert.True(new CreatePatientCommandValidator().Validate(command).IsValid);
    }

    [Fact]
    public void Validator_ContactoDeEmergenciaConCorreoInvalido_EsInvalido()
    {
        var command = Valid(
            emergencyContact: new EmergencyContactDto("Ana", null, "5551234567", "no-es-correo")
        );

        var result = new CreatePatientCommandValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "EmergencyContact.Email");
    }

    [Fact]
    public void Validator_SinContactoDeEmergencia_EsValido()
    {
        Assert.True(new CreatePatientCommandValidator().Validate(Valid()).IsValid);
    }

    private static void AssertMissing(
        Func<CreatePatientCommand, CreatePatientCommand> mutate,
        string propertyName
    )
    {
        var result = new CreatePatientCommandValidator().Validate(mutate(Valid()));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == propertyName);
    }
}
