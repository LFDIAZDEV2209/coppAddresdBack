using CoppAddresd.Application.Features.Professionals;
using FluentValidation;

namespace CoppAddresd.UnitTests.Features.Professionals;

public class ProfessionalOptionsTests
{
    [Theory]
    [InlineData("Invited")]
    [InlineData("Active")]
    [InlineData("Inactive")]
    public void IsAllowed_EmployeeStatuses_AceptaValoresValidos(string value)
        => Assert.True(ProfessionalOptions.IsAllowed(ProfessionalOptions.EmployeeStatuses, value));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void IsAllowed_ValorVacio_EsPermitido(string? value)
        => Assert.True(ProfessionalOptions.IsAllowed(ProfessionalOptions.EmployeeStatuses, value));

    [Fact]
    public void IsAllowed_EstadoDesconocido_Rechazado()
        => Assert.False(ProfessionalOptions.IsAllowed(ProfessionalOptions.EmployeeStatuses, "Suspendido"));

    [Fact]
    public void LicenseTypes_CubreTaxonomiaUsa()
    {
        Assert.Contains("StateLicense", ProfessionalOptions.LicenseTypes);
        Assert.Contains("BoardCertification", ProfessionalOptions.LicenseTypes);
        Assert.Contains("DeaRegistration", ProfessionalOptions.LicenseTypes);
        Assert.Contains("Npi", ProfessionalOptions.LicenseTypes);
    }
}

public class OrganizationCommandValidatorTests
{
    private readonly OrganizationCommandValidators.CreateOrganizationValidator _createOrgValidator = new();
    private readonly OrganizationCommandValidators.CreateClinicValidator _createClinicValidator = new();
    private readonly OrganizationCommandValidators.CreateLocationValidator _createLocationValidator = new();

    [Fact]
    public void CreateOrganization_CodigoVacio_Falla()
    {
        var command = new CreateOrganizationCommand("", "MediQuer");
        var result = _createOrgValidator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Code");
    }

    [Theory]
    [InlineData("mediquer")]
    [InlineData("medi-quer")]
    [InlineData("medi_quer2")]
    public void CreateOrganization_CodigoValido_Pasa(string code)
    {
        var command = new CreateOrganizationCommand(code, "MediQuer");
        Assert.True(_createOrgValidator.Validate(command).IsValid);
    }

    [Theory]
    [InlineData("MediQuer")]
    [InlineData("medi quer")]
    [InlineData("medi/quer")]
    public void CreateOrganization_CodigoConMayusculasOEspacios_Falla(string code)
    {
        var command = new CreateOrganizationCommand(code, "MediQuer");
        Assert.False(_createOrgValidator.Validate(command).IsValid);
    }

    [Fact]
    public void CreateClinic_NombreVacio_Falla()
    {
        var command = new CreateClinicCommand(Guid.NewGuid(), "", null);
        Assert.False(_createClinicValidator.Validate(command).IsValid);
    }

    [Fact]
    public void CreateLocation_ClinicIdVacio_Falla()
    {
        var command = new CreateLocationCommand(
            Guid.Empty, "Sede Centro", null, null, null, null, null, null, null);
        var result = _createLocationValidator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "ClinicId");
    }
}

public class EmployeeCommandValidatorTests
{
    private readonly EmployeeCommandValidators.CreateEmployeeValidator _createValidator = new();
    private readonly EmployeeCommandValidators.UpdateEmployeeValidator _updateValidator = new();

    private static CreateEmployeeCommand ComandoBase() => new(
        Guid.NewGuid(),
        "Ana",
        null,
        "López",
        "ana@mediquer.com",
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null);

    [Fact]
    public void Create_EmailInvalido_Falla()
    {
        var command = ComandoBase() with { Email = "no-es-correo" };
        var result = _createValidator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Email");
    }

    [Fact]
    public void Create_NombreFaltante_Falla()
    {
        var command = ComandoBase() with { FirstName = "" };
        Assert.False(_createValidator.Validate(command).IsValid);
    }

    [Fact]
    public void Create_EstadoEmpleadoInvalido_Falla()
    {
        var command = ComandoBase() with { Status = "Suspendido" };
        Assert.False(_createValidator.Validate(command).IsValid);
    }

    [Fact]
    public void Create_LicenciaTipoInvalido_Falla()
    {
        var command = ComandoBase() with
        {
            Licenses = [new LicenseInput("FakeType", null, "123", null, null, null, null, "Pending")],
        };
        Assert.False(_createValidator.Validate(command).IsValid);
    }

    [Fact]
    public void Create_LicenciaVerificacionInvalida_Falla()
    {
        var command = ComandoBase() with
        {
            Licenses = [new LicenseInput("StateLicense", null, "123", null, null, null, null, "OnFire")],
        };
        Assert.False(_createValidator.Validate(command).IsValid);
    }

    [Fact]
    public void Create_AsignacionClinicConEstadoInvalido_Falla()
    {
        var command = ComandoBase() with
        {
            Clinics = [new ClinicAssignmentInput(Guid.NewGuid(), true, "OnHold")],
        };
        Assert.False(_createValidator.Validate(command).IsValid);
    }

    [Fact]
    public void Create_PayloadMinimoValido_Pasa()
        => Assert.True(_createValidator.Validate(ComandoBase()).IsValid);

    [Fact]
    public void Update_EmailInvalido_Falla()
    {
        var command = new UpdateEmployeeCommand(
            Guid.NewGuid(), null, null, null, "mal-correo", null, null, null, null, null, null,
            null, null, null, null, null, false);
        Assert.False(_updateValidator.Validate(command).IsValid);
    }
}
