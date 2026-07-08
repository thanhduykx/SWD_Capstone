using CPMS.Core.Entities;
using CPMS.Application.DTOs.Accounts;
using CPMS.Shared.Models;

namespace CPMS.Application.Abstractions.Identity;

public interface IAccountRepository
{
    IQueryable<User> QueryUsers();
    Task<PagedResult<AccountResponse>> GetPagedAsync(PagedRequest request, CancellationToken cancellationToken);
    Task<bool> UsernameOrEmailExistsAsync(string username, string email, CancellationToken cancellationToken);
    Task AddUserWithProfileAsync(
        User user,
        object profile,
        AuditLog auditLog,
        CancellationToken cancellationToken);
}
