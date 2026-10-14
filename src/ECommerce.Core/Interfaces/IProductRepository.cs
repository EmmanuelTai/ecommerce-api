using ECommerce.Core.Entities;

namespace ECommerce.Core.Interfaces;

public interface IProductRepository : IGenericRepository<Product>
{
    // Takes stock only if enough is left. Returns false if the product is inactive or out of stock.
    Task<bool> TryDecreaseStockAsync(int productId, int quantity, CancellationToken cancellationToken = default);

    Task IncreaseStockAsync(int productId, int quantity, CancellationToken cancellationToken = default);
}
