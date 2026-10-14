using ECommerce.Core.Entities;
using ECommerce.Core.Interfaces;
using ECommerce.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore.Storage;

namespace ECommerce.Infrastructure.Data;

public class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    private IDbContextTransaction? _transaction;

    private IGenericRepository<User>? _users;
    private IGenericRepository<Category>? _categories;
    private IProductRepository? _products;
    private ICartRepository? _carts;
    private IOrderRepository? _orders;
    private IGenericRepository<Payment>? _payments;

    // Created on first use and all given the same DbContext, so they share one change tracker
    // and one connection. That shared context is what makes this a single unit of work.
    public IGenericRepository<User> Users => _users ??= new GenericRepository<User>(db);
    public IGenericRepository<Category> Categories => _categories ??= new GenericRepository<Category>(db);
    public IProductRepository Products => _products ??= new ProductRepository(db);
    public ICartRepository Carts => _carts ??= new CartRepository(db);
    public IOrderRepository Orders => _orders ??= new OrderRepository(db);
    public IGenericRepository<Payment> Payments => _payments ??= new GenericRepository<Payment>(db);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            throw new InvalidOperationException("A transaction is already in progress.");
        }

        _transaction = await db.Database.BeginTransactionAsync(cancellationToken);
    }

    // Saves pending changes before committing, so a service can't forget to save inside a transaction.
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            throw new InvalidOperationException("No transaction to commit.");
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await _transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    // Safe to call even if nothing was started. If a service throws without calling this, the transaction
    // is still rolled back when the DbContext is disposed at the end of the request.
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            return;
        }

        try
        {
            await _transaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }
}
