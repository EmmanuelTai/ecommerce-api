using ECommerce.Core.DTOs.Admin;
using ECommerce.Core.DTOs.Common;
using ECommerce.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Api.Controllers.Admin;

// Admins only, read-only. Password hashes are never selected, let alone returned.
[ApiController]
[Route("api/admin/customers")]
[Authorize(Roles = "Admin")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
public class AdminCustomersController(IAdminCustomerService customerService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<CustomerDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetCustomers([FromQuery] CustomerQuery query, CancellationToken cancellationToken)
    {
        var result = await customerService.GetCustomersAsync(query, cancellationToken);
        return Ok(ApiResponse.Ok(result.Items, result.Meta));
    }
}
