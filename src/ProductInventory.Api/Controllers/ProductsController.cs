using Microsoft.AspNetCore.Mvc;
using ProductInventory.Application.Interfaces;
using ProductInventory.Contracts;

namespace ProductInventory.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(IProductService productService, ILogger<ProductsController> logger)
    {
        _productService = productService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<ProductResponse>>> GetProducts(
        [FromQuery] ProductSearchRequest request,
        CancellationToken ct)
    {
        var result = await _productService.GetProductsAsync(request, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> GetById(Guid id, CancellationToken ct)
    {
        var product = await _productService.GetByIdAsync(id, ct);
        return Ok(product);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<InventorySummaryResponse>> GetSummary(CancellationToken ct)
    {
        var summary = await _productService.GetSummaryAsync(ct);
        return Ok(summary);
    }

    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create([FromBody] ProductUpsertRequest request, CancellationToken ct)
    {
        var product = await _productService.CreateAsync(request, ct);
        _logger.LogInformation("Created product {ProductId}", product.Id);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> Update(Guid id, [FromBody] ProductUpsertRequest request, CancellationToken ct)
    {
        var product = await _productService.UpdateAsync(id, request, ct);
        _logger.LogInformation("Updated product {ProductId}", id);
        return Ok(product);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _productService.DeleteAsync(id, ct);
        _logger.LogInformation("Deleted product {ProductId}", id);
        return NoContent();
    }
}
