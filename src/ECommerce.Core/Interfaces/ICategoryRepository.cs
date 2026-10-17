using ECommerce.Core.DTOs.Catalog;
using ECommerce.Core.Entities;

namespace ECommerce.Core.Interfaces;

public interface ICategoryRepository : IGenericRepository<Category>
{
    // Read-only, projected straight to DTOs, ordered by name.
    Task<IReadOnlyList<CategoryDto>> GetAllDtosAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);

    // excludeId: the category being renamed, which may keep its own slug.
    Task<bool> SlugExistsAsync(string slug, int? excludeId = null, CancellationToken cancellationToken = default);

    // Inactive products count too: they still point at the category, so it can't be deleted.
    Task<bool> HasProductsAsync(int categoryId, CancellationToken cancellationToken = default);
}
