using ECommerce.Core.DTOs.Cart;
using ECommerce.Core.DTOs.Common;
using ECommerce.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Api.Controllers;

// Customers only: no token → 401, an admin token → 403. The cart is always the caller's own,
// so no route contains a cart or user ID.
[ApiController]
[Route("api/cart")]
[Authorize(Roles = "Customer")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
public class CartController(ICartService cartService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<CartDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCart(CancellationToken cancellationToken)
    {
        var result = await cartService.GetCartAsync(cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    // 200 with the updated cart rather than 201: the cart already exists and this changes it
    // (adding a product that's already there only increases its quantity).
    [HttpPost("items")]
    [ProducesResponseType<ApiResponse<CartDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddItem(AddCartItemRequest request, CancellationToken cancellationToken)
    {
        var result = await cartService.AddItemAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPut("items/{itemId:int}")]
    [ProducesResponseType<ApiResponse<CartDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateItem(int itemId, UpdateCartItemRequest request, CancellationToken cancellationToken)
    {
        var result = await cartService.UpdateItemAsync(itemId, request, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpDelete("items/{itemId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveItem(int itemId, CancellationToken cancellationToken)
    {
        await cartService.RemoveItemAsync(itemId, cancellationToken);
        return NoContent();
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken)
    {
        await cartService.ClearAsync(cancellationToken);
        return NoContent();
    }
}
