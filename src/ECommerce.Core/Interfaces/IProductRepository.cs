using ECommerce.Core.DTOs.Admin;
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

    // Admin (T11). Read-only: one page of products, active and inactive.
    Task<PagedResult<AdminProductDto>> GetAdminDtosAsync(AdminProductQuery query, CancellationToken cancellationToken = default);

    // Read-only: null only if the product doesn't exist (inactive ones are returned).
    Task<AdminProductDto?> GetAdminDtoByIdAsync(int id, CancellationToken cancellationToken = default);

    // excludeId: the product being updated, which may keep its own slug.
    Task<bool> SlugExistsAsync(string slug, int? excludeId = null, CancellationToken cancellationToken = default);

    // Every product in the category, active or not: used to clear their cached copies when the category is renamed.
    Task<IReadOnlyList<int>> GetIdsByCategoryIdAsync(int categoryId, CancellationToken cancellationToken = default);

    // Active products with stock at or below the threshold, lowest first.
    Task<IReadOnlyList<LowStockProductDto>> GetLowStockAsync(int threshold, int count, CancellationToken cancellationToken = default);
}
