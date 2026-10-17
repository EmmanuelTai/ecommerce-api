using ECommerce.Core.DTOs.Admin;
using ECommerce.Core.DTOs.Common;
using ECommerce.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Api.Controllers.Admin;

// Admins only. Unlike /api/orders, these see every customer's orders.
[ApiController]
[Route("api/admin/orders")]
[Authorize(Roles = "Admin")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
public class AdminOrdersController(IAdminOrderService orderService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<AdminOrderSummaryDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetOrders([FromQuery] AdminOrderQuery query, CancellationToken cancellationToken)
    {
        var result = await orderService.GetOrdersAsync(query, cancellationToken);
        return Ok(ApiResponse.Ok(result.Items, result.Meta));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<ApiResponse<AdminOrderDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await orderService.GetOrderAsync(id, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    // A move the status flow doesn't allow (e.g. Delivered → Pending) → 409.
    [HttpPut("{id:int}/status")]
    [ProducesResponseType<ApiResponse<AdminOrderDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStatus(int id, UpdateOrderStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await orderService.UpdateStatusAsync(id, request, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }
}
