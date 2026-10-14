using ECommerce.Core.DTOs.Catalog;
using ECommerce.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ECommerce.Core.Services;

public class CategoryService(IUnitOfWork unitOfWork, IMemoryCache cache) : ICategoryService
{
    // Categories are read on almost every page and change only when an admin edits them (which clears this
    // entry in T11), so a long cache time is safe. The expiry is a backstop in case a clear is ever missed.
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKeys.Categories, out IReadOnlyList<CategoryDto>? cached) && cached is not null)
        {
            return cached;
        }

        var categories = await unitOfWork.Categories.GetAllDtosAsync(cancellationToken);
        cache.Set(CacheKeys.Categories, categories, CacheDuration);
        return categories;
    }
}
