using System.Security.Claims;
using CoppAddresd.Api.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace CoppAddresd.UnitTests.Api.Authorization;

/// <summary>
/// Pruebas de las políticas de audiencia del módulo SOS (task 3.2, D8):
/// activación/consulta/cancelación solo aud=app; atención solo aud=erp.
/// El catch-all YARP del Gateway NO relaja la política: se aplica en el API.
/// </summary>
public sealed class SosAudiencePolicyTests
{
    private readonly AudienceAuthorizationHandler _handler = new();

    [Fact]
    public async Task PoliticaAppPatient_TokenDeApp_Pasa()
    {
        Assert.True(await AllowedAsync(SosPolicies.AppPatient, "app"));
    }

    [Theory]
    [InlineData("erp")]
    [InlineData(null)]
    public async Task PoliticaAppPatient_TokenErpOAnonimoConAudenciaErrada_Rechaza(string? audience)
    {
        // (D8) Un token del ERP llamando POST /api/v1/sos/alerts → 403.
        Assert.False(await AllowedAsync(SosPolicies.AppPatient, audience));
    }

    [Fact]
    public async Task PoliticaErpStaff_TokenDeErp_Pasa()
    {
        Assert.True(await AllowedAsync(SosPolicies.ErpStaff, "erp"));
    }

    [Theory]
    [InlineData("app")]
    [InlineData(null)]
    public async Task PoliticaErpStaff_TokenDeApp_Rechaza(string? audience)
    {
        // (D8) aud=app intentando attend → 403.
        Assert.False(await AllowedAsync(SosPolicies.ErpStaff, audience));
    }

    [Fact]
    public async Task SinIdentidadAutenticada_NingunaPoliticaPasa()
    {
        // 401 lo emite el middleware de autenticación; el handler no otorga
        // éxito a un principal no autenticado (defensa en profundidad).
        var requirement = new AudienceRequirement("app");
        var context = new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(new ClaimsIdentity()), // no autenticado
            null
        );

        await _handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    private async Task<bool> AllowedAsync(string policyName, string? audience)
    {
        var requirement = new AudienceRequirement(
            policyName == SosPolicies.AppPatient ? "app" : "erp"
        );
        var claims = audience is null ? new List<Claim>() : [new Claim("aud", audience)];
        var context = new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(new ClaimsIdentity(claims, "test")),
            null
        );

        await _handler.HandleAsync(context);
        return context.HasSucceeded;
    }
}
