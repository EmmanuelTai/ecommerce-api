using ECommerce.Core.DTOs.Catalog;

namespace ECommerce.Core.Interfaces;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken cancellationToken = default);
}
