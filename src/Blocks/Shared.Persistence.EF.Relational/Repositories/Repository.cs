using Microsoft.EntityFrameworkCore;

using QltS.Core.Domain.SeedWork;

using System.Linq.Expressions;

namespace QltS.Shared.Persistence.EF.Relational.Repositories;

/// <summary>
/// Generic repository implementation that works with any DbContext.
/// Provides common CRUD operations for aggregate roots.
/// </summary>
/// <typeparam name="T">The aggregate root entity type</typeparam>
public abstract class Repository<T> : IRepository<T> where T : Entity, IAggregateRoot
{
    protected readonly DbContext Context;

    public IUnitOfWork UnitOfWork => (IUnitOfWork)Context;

    protected Repository(DbContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public T Add(T entity)
    {
        return Context.Add(entity).Entity;
    }

    public void Update(T entity)
    {
        Context.Update(entity);
    }

    public async Task<T> GetAsync(Guid id)
    {
        return await Context.Set<T>()
            .SingleOrDefaultAsync(e => e.Id == id);
    }

    // Implementación de métodos que estaban comentados en IRepository
    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        return await Context.Set<T>()
            .Where(predicate)
            .ToListAsync();
    }

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
    {
        return await Context.Set<T>()
            .FirstOrDefaultAsync(predicate);
    }
}
