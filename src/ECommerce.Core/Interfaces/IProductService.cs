using ECommerce.Core.DTOs.Catalog;
using ECommerce.Core.DTOs.Common;

namespace ECommerce.Core.Interfaces;

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetProductsAsync(ProductQuery query, CancellationToken cancellationToken = default);
    Task<ProductDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}
