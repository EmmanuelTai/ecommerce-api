using ECommerce.Core.Interfaces;
using ECommerce.Infrastructure.Data;

namespace ECommerce.Infrastructure.Repositories;

public class GenericRepository<T>(AppDbContext db) : IGenericRepository<T> where T : class
{
    // Protected so the specific repositories can build their own queries on the same context.
    protected readonly AppDbContext Db = db;

    public async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await Db.Set<T>().FindAsync([id], cancellationToken);

    public void Add(T entity) => Db.Set<T>().Add(entity);

    // Only needed for entities that aren't tracked yet; changes to tracked entities are detected automatically.
    public void Update(T entity) => Db.Set<T>().Update(entity);

    public void Remove(T entity) => Db.Set<T>().Remove(entity);
}
