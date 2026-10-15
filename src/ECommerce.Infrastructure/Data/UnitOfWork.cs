using ECommerce.Core.Entities;
using ECommerce.Core.Exceptions;
using ECommerce.Core.Interfaces;
using ECommerce.Infrastructure.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ECommerce.Infrastructure.Data;

public class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    private IDbContextTransaction? _transaction;

    private IUserRepository? _users;
    private ICategoryRepository? _categories;
    private IProductRepository? _products;
    private ICartRepository? _carts;
    private IOrderRepository? _orders;
    private IPaymentRepository? _payments;

    // Created on first use and all given the same DbContext, so they share one change tracker
    // and one connection. That shared context is what makes this a single unit of work.
    public IUserRepository Users => _users ??= new UserRepository(db);
    public ICategoryRepository Categories => _categories ??= new CategoryRepository(db);
    public IProductRepository Products => _products ??= new ProductRepository(db);
    public ICartRepository Carts => _carts ??= new CartRepository(db);
    public IOrderRepository Orders => _orders ??= new OrderRepository(db);
    public IPaymentRepository Payments => _payments ??= new PaymentRepository(db);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await db.SaveChangesAsync(cancellationToken);
        }
        // SQL Server errors 2601/2627 = a unique index or constraint was violated. A service's "already exists"
        // check can lose a race (two requests both check, both see nothing, both insert); the unique index is
        // the real guarantee. Translating it here keeps EF and SqlClient types out of Core, and gives 409, not 500.
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException("A record with the same unique value already exists.");
        }
        // EF expected to update or delete a row and found it already gone or changed, e.g. the same customer
        // placing an order twice at once: the second request's cart lines were already deleted by the first.
        // It's a race lost to another request, so 409 ("try again"), not a 500.
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("This data was changed by another request. Please try again.");
        }
    }

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
            await SaveChangesAsync(cancellationToken);
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
