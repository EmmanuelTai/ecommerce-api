using ECommerce.Core.DTOs.Catalog;
using ECommerce.Core.DTOs.Common;
using ECommerce.Core.Entities;

namespace ECommerce.Core.Interfaces;

public interface IProductRepository : IGenericRepository<Product>
{
    // Read-only: one filtered, sorted page of active products, projected to DTOs.
    Task<PagedResult<ProductDto>> GetActiveDtosAsync(ProductQuery query, CancellationToken cancellationToken = default);

    // Read-only: null if the product doesn't exist or is inactive.
    Task<ProductDto?> GetActiveDtoByIdAsync(int id, CancellationToken cancellationToken = default);

    // Takes stock only if enough is left. Returns false if the product is inactive or out of stock.
    Task<bool> TryDecreaseStockAsync(int productId, int quantity, CancellationToken cancellationToken = default);

    Task IncreaseStockAsync(int productId, int quantity, CancellationToken cancellationToken = default);
}
