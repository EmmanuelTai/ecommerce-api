namespace ECommerce.Core.Interfaces;

// Basic CRUD shared by every entity. Add/Update/Remove only stage changes;
// nothing reaches the database until IUnitOfWork.SaveChangesAsync is called.
// There is no IQueryable here on purpose: Core has no EF Core reference, so services couldn't
// run it asynchronously. Queries live in the specific repositories instead.
public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    void Add(T entity);
    void Update(T entity);
    void Remove(T entity);
}
