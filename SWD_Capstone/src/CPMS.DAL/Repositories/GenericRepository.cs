using CPMS.BLL.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CPMS.DAL.Repositories;

public class GenericRepository<TEntity, TKey>(DbContext dbContext) : IRepository<TEntity, TKey>
    where TEntity : class
{
    protected DbSet<TEntity> Set => dbContext.Set<TEntity>();

    public async Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default) =>
        await Set.FindAsync([id], cancellationToken);

    public IQueryable<TEntity> Query() => Set.AsQueryable();

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        await Set.AddAsync(entity, cancellationToken);

    public void Update(TEntity entity) => Set.Update(entity);

    public void Remove(TEntity entity) => Set.Remove(entity);
}
