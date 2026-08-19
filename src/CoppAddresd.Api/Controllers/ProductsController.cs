using CoppAddresd.Application.Features.Inventory;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

[ApiController]
[Route("api/v1/inventory/[controller]")]
[Authorize]
public class ProductsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedProductsResult>> List(
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await mediator.Send(new ListProductsQuery(search, category, status, page, pageSize), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetProductQuery(id), ct);
        return result is null ? NotFound(new { message = "Producto no encontrado" }) : Ok(result);
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<string>>> ListCategories(CancellationToken ct)
        => Ok(await mediator.Send(new ListProductCategoriesQuery(), ct));

    [HttpGet("suppliers")]
    public async Task<ActionResult<IReadOnlyList<string>>> ListSuppliers(CancellationToken ct)
        => Ok(await mediator.Send(new ListProductSuppliersQuery(), ct));

    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(
        [FromBody] CreateProductRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateProductCommand(request), ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductDto>> Update(
        Guid id, [FromBody] UpdateProductRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateProductCommand(id, request), ct);
        return result is null ? NotFound(new { message = "Producto no encontrado" }) : Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await mediator.Send(new DeleteProductCommand(id), ct);
        return deleted ? NoContent() : NotFound(new { message = "Producto no encontrado" });
    }
}
