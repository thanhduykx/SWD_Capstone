using CPMS.BLL.Abstractions.Identity;
using CPMS.BLL.DTOs.Accounts;
using CPMS.Shared.Models;

namespace CPMS.BLL.Features.Accounts.Queries.GetAccounts;

public sealed class GetAccountsQueryHandler(IAccountRepository accounts)
{
    public async Task<PagedResult<AccountResponse>> HandleAsync(
        PagedRequest request,
        CancellationToken cancellationToken) =>
        await accounts.GetPagedAsync(request, cancellationToken);
}
