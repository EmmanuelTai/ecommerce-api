using ECommerce.Core.DTOs.Admin;
using ECommerce.Core.DTOs.Catalog;
using ECommerce.Core.DTOs.Common;

namespace ECommerce.Core.Interfaces;

// Admin-only product and category changes. Every change clears the cache entries it makes stale.
public interface IAdminCatalogService
{
    Task<PagedResult<AdminProductDto>> GetProductsAsync(AdminProductQuery query, CancellationToken cancellationToken = default);
    Task<AdminProductDto> GetProductAsync(int id, CancellationToken cancellationToken = default);
    Task<AdminProductDto> CreateProductAsync(SaveProductRequest request, CancellationToken cancellationToken = default);
    Task<AdminProductDto> UpdateProductAsync(int id, SaveProductRequest request, CancellationToken cancellationToken = default);
    Task DeleteProductAsync(int id, CancellationToken cancellationToken = default);

    Task<CategoryDto> CreateCategoryAsync(SaveCategoryRequest request, CancellationToken cancellationToken = default);
    Task<CategoryDto> UpdateCategoryAsync(int id, SaveCategoryRequest request, CancellationToken cancellationToken = default);
    Task DeleteCategoryAsync(int id, CancellationToken cancellationToken = default);
}
