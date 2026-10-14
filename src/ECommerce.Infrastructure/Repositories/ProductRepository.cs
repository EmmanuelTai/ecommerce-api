using ECommerce.Core.Entities;
using ECommerce.Core.Interfaces;
using ECommerce.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Repositories;

public class ProductRepository(AppDbContext db) : GenericRepository<Product>(db), IProductRepository
{
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
