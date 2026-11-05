using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace CoppAddresd.Api.Authorization;

public static class NutritionPolicies
{
    public const string Staff = "NutritionErpStaff";
    public static void Configure(AuthorizationPolicyBuilder policy) => policy.RequireAuthenticatedUser()
        .RequireClaim("aud", "erp")
        .RequireAssertion(c => Guid.TryParse(c.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && userId != Guid.Empty);
}
