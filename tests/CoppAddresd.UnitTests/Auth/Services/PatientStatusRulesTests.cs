using CoppAddresd.Auth.Security;

namespace CoppAddresd.UnitTests.Auth.Services;

/// <summary>
/// Regla de bloqueo del acceso a la app móvil: SOLO el estado exacto
/// "Inactivo" (ordinal) bloquea. Null, vacío o cualquier otro estado se
/// permite para no dejar fuera a filas legadas sin estado.
/// </summary>
public sealed class PatientStatusRulesTests
{
    [Theory]
    [InlineData("Inactivo", true)]
    [InlineData("inactivo", false)]
    [InlineData("INACTIVO", false)]
    [InlineData(" Inactivo", false)]
    [InlineData("Inactivo ", false)]
    [InlineData("Inactivoo", false)]
    [InlineData("Activo", false)]
    [InlineData("Pendiente", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void BlocksAppAccess_SoloEstadoInactivoExacto(string? status, bool expected)
    {
        Assert.Equal(expected, PatientStatusRules.BlocksAppAccess(status));
    }
}
