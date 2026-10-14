using ECommerce.Core.Entities;

namespace ECommerce.Core.Interfaces;

// One entry point for all data access in a request. Every repository shares the same DbContext,
// so a single SaveChangesAsync (or transaction) covers changes made through any of them.
public interface IUnitOfWork
{
    IUserRepository Users { get; }
    ICategoryRepository Categories { get; }
    IProductRepository Products { get; }
    ICartRepository Carts { get; }
    IOrderRepository Orders { get; }
    IGenericRepository<Payment> Payments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    // For work that mixes SaveChanges with direct SQL (e.g. conditional stock updates),
    // which must succeed or fail together.
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
