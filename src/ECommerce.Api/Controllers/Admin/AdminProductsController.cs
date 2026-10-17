using ECommerce.Core.DTOs.Admin;
using ECommerce.Core.DTOs.Common;
using ECommerce.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Api.Controllers.Admin;

// Admins only: no token → 401, a customer token → 403.
[ApiController]
[Route("api/admin/products")]
[Authorize(Roles = "Admin")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
public class AdminProductsController(IAdminCatalogService catalogService) : ControllerBase
{
    // Active and inactive; ?isActive=false lists only the soft-deleted ones.
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<AdminProductDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetProducts([FromQuery] AdminProductQuery query, CancellationToken cancellationToken)
    {
        var result = await catalogService.GetProductsAsync(query, cancellationToken);
        return Ok(ApiResponse.Ok(result.Items, result.Meta));
    }

    // Also the Location target for POST. Unlike /api/products/{id}, inactive products are returned.
    [HttpGet("{id:int}")]
    [ProducesResponseType<ApiResponse<AdminProductDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await catalogService.GetProductAsync(id, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost]
    [ProducesResponseType<ApiResponse<AdminProductDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(SaveProductRequest request, CancellationToken cancellationToken)
    {
        var result = await catalogService.CreateProductAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse.Ok(result));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<ApiResponse<AdminProductDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, SaveProductRequest request, CancellationToken cancellationToken)
    {
        var result = await catalogService.UpdateProductAsync(id, request, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    // Soft delete (IsActive = false). A PUT with "isActive": true brings it back.
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await catalogService.DeleteProductAsync(id, cancellationToken);
        return NoContent();
    }
}
