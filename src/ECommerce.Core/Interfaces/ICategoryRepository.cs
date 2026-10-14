using ECommerce.Core.DTOs.Catalog;
using ECommerce.Core.Entities;

namespace ECommerce.Core.Interfaces;

public interface ICategoryRepository : IGenericRepository<Category>
{
    // Read-only, projected straight to DTOs, ordered by name.
    Task<IReadOnlyList<CategoryDto>> GetAllDtosAsync(CancellationToken cancellationToken = default);
}
