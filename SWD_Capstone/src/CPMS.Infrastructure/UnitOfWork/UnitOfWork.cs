using CPMS.Application.Abstractions.Persistence;
using CPMS.Infrastructure.Data;

namespace CPMS.Infrastructure.UnitOfWork;

public sealed class UnitOfWork(CpmsDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
