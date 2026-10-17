using ECommerce.Core.DTOs.Admin;
using ECommerce.Core.DTOs.Catalog;
using ECommerce.Core.DTOs.Common;
using ECommerce.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Api.Controllers.Admin;

// Admins only. Reading categories is the public GET /api/categories; admins have nothing extra to see.
[ApiController]
[Route("api/admin/categories")]
[Authorize(Roles = "Admin")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
public class AdminCategoriesController(IAdminCatalogService catalogService) : ControllerBase
{
    // Location points at the public list, the only place a category can be read.
    [HttpPost]
    [ProducesResponseType<ApiResponse<CategoryDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(SaveCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await catalogService.CreateCategoryAsync(request, cancellationToken);
        return Created("/api/categories", ApiResponse.Ok(result));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<ApiResponse<CategoryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, SaveCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await catalogService.UpdateCategoryAsync(id, request, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    // A real delete (categories are not soft-deleted), so only allowed when no product uses it; otherwise 409.
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await catalogService.DeleteCategoryAsync(id, cancellationToken);
        return NoContent();
    }
}
