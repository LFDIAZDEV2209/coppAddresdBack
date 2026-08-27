using CoppAddresd.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[AllowAnonymous]
public class FoodAiController : ControllerBase
{
    private readonly IFoodAiClient _foodAiClient;

    public FoodAiController(IFoodAiClient foodAiClient)
    {
        _foodAiClient = foodAiClient;
    }

    /// <summary>
    /// Prueba de comunicación backend → Food AI Service. Health público:
    /// no expone datos sensibles.
    /// </summary>
    [HttpGet("health")]
    public async Task<ActionResult> Health(CancellationToken ct)
    {
        var status = await _foodAiClient.GetHealthAsync(ct);
        return Ok(new
        {
            backend = "healthy",
            foodAI = status.IsHealthy ? "healthy" : "unhealthy",
            detail = status.Detail,
        });
    }
}