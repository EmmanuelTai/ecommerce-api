using ECommerce.Core.DTOs.Catalog;
using ECommerce.Core.DTOs.Common;
using ECommerce.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Api.Controllers;

// Public: no [Authorize], anyone can browse.
[ApiController]
[Route("api/categories")]
public class CategoriesController(ICategoryService categoryService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<CategoryDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await categoryService.GetAllAsync(cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }
}
