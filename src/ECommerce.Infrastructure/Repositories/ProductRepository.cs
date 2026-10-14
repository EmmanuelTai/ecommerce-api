using System.Linq.Expressions;
using ECommerce.Core.DTOs.Catalog;
using ECommerce.Core.DTOs.Common;
using ECommerce.Core.Entities;
using ECommerce.Core.Interfaces;
using ECommerce.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Repositories;

public class ProductRepository(AppDbContext db) : GenericRepository<Product>(db), IProductRepository
{
    // An expression (not a method) so EF can translate it into SQL: it selects only these columns and joins
    // Categories for the name by itself, so no Include is needed. Shared by the list and the single-product query.
    private static readonly Expression<Func<Product, ProductDto>> ToDto = p => new ProductDto(
        p.Id, p.Name, p.Slug, p.Description, p.Price, p.StockQuantity, p.ImageUrl,
        p.CategoryId, p.Category.Name, p.CreatedAt);

    public async Task<PagedResult<ProductDto>> GetActiveDtosAsync(ProductQuery query, CancellationToken cancellationToken = default)
    {
        // AsNoTracking: these are read-only, so skip the change tracker's snapshot of every row.
        // Each filter is only added when it was sent; nothing runs until CountAsync/ToListAsync.
        var products = Db.Products.AsNoTracking().Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Becomes a parameterised LIKE '%...%', so the search text can't inject SQL.
            var search = query.Search.Trim();
            products = products.Where(p => p.Name.Contains(search) || p.Description.Contains(search));
        }

        if (query.CategoryId is not null)
        {
            products = products.Where(p => p.CategoryId == query.CategoryId);
        }

        if (query.MinPrice is not null)
        {
            products = products.Where(p => p.Price >= query.MinPrice);
        }

        if (query.MaxPrice is not null)
        {
            products = products.Where(p => p.Price <= query.MaxPrice);
        }

        // Id as the tie-breaker: without a unique order, SQL may return equal-priced rows in a different
        // order on each request, so an item could show up on two pages or none.
        products = query.SortBy switch
        {
            ProductQuery.SortPriceAsc => products.OrderBy(p => p.Price).ThenBy(p => p.Id),
            ProductQuery.SortPriceDesc => products.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
            _ => products.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
        };

        // Two queries: the total (for meta) and the page itself.
        var totalCount = await products.CountAsync(cancellationToken);
        var items = await products
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ToDto)
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductDto>(items, new PaginationMeta(query.Page, query.PageSize, totalCount));
    }

    public Task<ProductDto?> GetActiveDtoByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Db.Products
            .AsNoTracking()
            .Where(p => p.Id == id && p.IsActive)
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> TryDecreaseStockAsync(int productId, int quantity, CancellationToken cancellationToken = default)
    {
        // One conditional UPDATE instead of "read stock, check, then save": the check and the change
        // happen in the same statement, so two buyers can't both take the last item.
        // ExecuteUpdateAsync skips SaveChanges, so UpdatedAt is set here by hand.
        var rows = await Db.Products
            .Where(p => p.Id == productId && p.IsActive && p.StockQuantity >= quantity)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.StockQuantity, p => p.StockQuantity - quantity)
                .SetProperty(p => p.UpdatedAt, DateTime.UtcNow),
                cancellationToken);

        return rows == 1;
    }

    public async Task IncreaseStockAsync(int productId, int quantity, CancellationToken cancellationToken = default)
    {
        await Db.Products
            .Where(p => p.Id == productId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.StockQuantity, p => p.StockQuantity + quantity)
                .SetProperty(p => p.UpdatedAt, DateTime.UtcNow),
                cancellationToken);
    }
}
