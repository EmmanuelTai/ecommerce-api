using ECommerce.Core.DTOs.Catalog;
using ECommerce.Core.DTOs.Common;
using ECommerce.Core.Exceptions;
using ECommerce.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ECommerce.Core.Services;

public class ProductService(IUnitOfWork unitOfWork, IMemoryCache cache) : IProductService
{
    // Shorter than categories: the stock count shown here goes stale as people buy. That's fine for display,
    // because cart and checkout always check stock against the database, never against this cache.
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    // Not cached: every mix of page, search, filters and sort would be its own entry, and most would
    // rarely be hit again. At this catalogue size the query is cheap, so going to the database is fine.
    public Task<PagedResult<ProductDto>> GetProductsAsync(ProductQuery query, CancellationToken cancellationToken = default)
    {
        if (query.MinPrice > query.MaxPrice)
        {
            throw new BadRequestException("minPrice cannot be greater than maxPrice.");
        }

        return unitOfWork.Products.GetActiveDtosAsync(query, cancellationToken);
    }

    public async Task<ProductDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var key = CacheKeys.Product(id);
        if (cache.TryGetValue(key, out ProductDto? cached) && cached is not null)
        {
            return cached;
        }

        // Missing and inactive products both give 404. Only found products are cached, so a product
        // that is created or reactivated later shows up straight away.
        var product = await unitOfWork.Products.GetActiveDtoByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Product not found.");

        cache.Set(key, product, CacheDuration);
        return product;
    }
}
