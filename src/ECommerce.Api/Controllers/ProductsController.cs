using ECommerce.Core.DTOs.Catalog;
using ECommerce.Core.DTOs.Common;
using ECommerce.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Api.Controllers;

// Public: no [Authorize], anyone can browse. Only active products are ever returned.
[ApiController]
[Route("api/products")]
public class ProductsController(IProductService productService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<ProductDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetProducts([FromQuery] ProductQuery query, CancellationToken cancellationToken)
    {
        var result = await productService.GetProductsAsync(query, cancellationToken);
        return Ok(ApiResponse.Ok(result.Items, result.Meta));
    }

    // :int means /api/products/abc doesn't match this route at all (404) instead of failing to bind.
    [HttpGet("{id:int}")]
    [ProducesResponseType<ApiResponse<ProductDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await productService.GetByIdAsync(id, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }
}
