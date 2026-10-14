using ECommerce.Core.DTOs.Catalog;
using ECommerce.Core.Entities;
using ECommerce.Core.Interfaces;
using ECommerce.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Repositories;

public class CategoryRepository(AppDbContext db) : GenericRepository<Category>(db), ICategoryRepository
{
    public async Task<IReadOnlyList<CategoryDto>> GetAllDtosAsync(CancellationToken cancellationToken = default) =>
        await Db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Slug))
            .ToListAsync(cancellationToken);
}
