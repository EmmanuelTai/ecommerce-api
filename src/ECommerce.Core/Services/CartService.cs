using ECommerce.Core.DTOs.Cart;
using ECommerce.Core.Entities;
using ECommerce.Core.Exceptions;
using ECommerce.Core.Interfaces;

namespace ECommerce.Core.Services;

// Not cached: a cart changes with almost every request and belongs to one user, so a cache would rarely be hit
// and would need clearing on every change.
public class CartService(IUnitOfWork unitOfWork, ICurrentUserService currentUser) : ICartService
{
    public async Task<CartDto> GetCartAsync(CancellationToken cancellationToken = default)
    {
        var items = await unitOfWork.Carts.GetItemDtosByUserIdAsync(currentUser.UserId, cancellationToken);
        var total = items.Where(i => i.IsAvailable).Sum(i => i.Subtotal);
        return new CartDto(items, total);
    }

    public async Task<CartDto> AddItemAsync(AddCartItemRequest request, CancellationToken cancellationToken = default)
    {
        var cart = await GetTrackedCartAsync(cancellationToken);

        // Loaded from the database through the UnitOfWork, NOT through ProductService: its cached copy can be
        // up to 10 minutes old, and checking stock against stale numbers would let customers add sold-out items.
        var product = await unitOfWork.Products.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException("Product not found.");

        if (!product.IsActive)
        {
            throw new BadRequestException($"{product.Name} is no longer available.");
        }

        // Adding a product that's already in the cart increases that line instead of adding a second one.
        var item = cart.Items.FirstOrDefault(i => i.ProductId == product.Id);
        var newQuantity = (item?.Quantity ?? 0) + request.Quantity;
        EnsureInStock(product, newQuantity);

        if (item is null)
        {
            // The cart is tracked, so SaveChanges sees the new line and fills in CartId.
            // If two requests add the same product at the same moment, the unique (CartId, ProductId) index
            // stops the second insert and UnitOfWork turns it into a 409.
            cart.Items.Add(new CartItem { ProductId = product.Id, Quantity = newQuantity });
        }
        else
        {
            item.Quantity = newQuantity;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetCartAsync(cancellationToken);
    }

    public async Task<CartDto> UpdateItemAsync(int itemId, UpdateCartItemRequest request, CancellationToken cancellationToken = default)
    {
        var cart = await GetTrackedCartAsync(cancellationToken);
        var item = FindItem(cart, itemId);

        if (request.Quantity == 0)
        {
            cart.Items.Remove(item);
        }
        else
        {
            // item.Product was loaded from the database together with the cart (Include), not from the cache,
            // so this checks the current stock.
            if (!item.Product.IsActive)
            {
                throw new BadRequestException($"{item.Product.Name} is no longer available. Remove it from your cart.");
            }

            EnsureInStock(item.Product, request.Quantity);
            item.Quantity = request.Quantity;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetCartAsync(cancellationToken);
    }

    public async Task RemoveItemAsync(int itemId, CancellationToken cancellationToken = default)
    {
        var cart = await GetTrackedCartAsync(cancellationToken);
        cart.Items.Remove(FindItem(cart, itemId));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        var cart = await GetTrackedCartAsync(cancellationToken);
        cart.Items.Clear();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // Tracked, because every caller changes it. Removing an item from cart.Items is enough to delete it:
    // CartItem.CartId is required, so EF deletes the orphaned row on SaveChanges.
    // Every customer gets a cart when they register (or are seeded), so "not found" shouldn't happen.
    private async Task<Cart> GetTrackedCartAsync(CancellationToken cancellationToken) =>
        await unitOfWork.Carts.GetByUserIdWithItemsAsync(currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException("Cart not found.");

    // Searched only inside the current user's cart, so another customer's item ID gives the same 404
    // as an ID that doesn't exist, and reveals nothing about other carts.
    private static CartItem FindItem(Cart cart, int itemId) =>
        cart.Items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new NotFoundException("Cart item not found.");

    // A friendly early check. Stock can still change between now and checkout; the conditional UPDATE
    // at checkout (T8) is what actually prevents overselling.
    // 409, not 400: the request is valid, it just conflicts with the current stock. Checkout uses 409 for the same case.
    private static void EnsureInStock(Product product, int quantity)
    {
        if (quantity > product.StockQuantity)
        {
            throw new ConflictException(product.StockQuantity == 0
                ? $"{product.Name} is out of stock."
                : $"Only {product.StockQuantity} of {product.Name} left in stock.");
        }
    }
}
