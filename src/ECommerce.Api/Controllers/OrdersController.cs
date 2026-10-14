using ECommerce.Core.DTOs.Common;
using ECommerce.Core.DTOs.Orders;
using ECommerce.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Api.Controllers;

// Customers only. Order IDs in routes are always checked against the current user (someone else's → 404).
[ApiController]
[Route("api/orders")]
[Authorize(Roles = "Customer")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
public class OrdersController(IOrderService orderService) : ControllerBase
{
    // Checkout: turns the current cart into a Pending order.
    [HttpPost]
    [ProducesResponseType<ApiResponse<OrderDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PlaceOrder(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await orderService.PlaceOrderAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse.Ok(result));
    }

    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<OrderSummaryDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMyOrders([FromQuery] OrderQuery query, CancellationToken cancellationToken)
    {
        var result = await orderService.GetMyOrdersAsync(query, cancellationToken);
        return Ok(ApiResponse.Ok(result.Items, result.Meta));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<ApiResponse<OrderDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await orderService.GetMyOrderAsync(id, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    // POST, not DELETE: the order isn't removed, it moves to Cancelled and stays in the history.
    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType<ApiResponse<OrderDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var result = await orderService.CancelAsync(id, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }
}
