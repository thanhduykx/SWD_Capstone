using CPMS.BLL.Abstractions.Persistence;
using CPMS.DAL.Data;

namespace CPMS.DAL.UnitOfWork;

public sealed class UnitOfWork(CpmsDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
