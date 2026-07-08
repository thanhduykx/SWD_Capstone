using CPMS.Application.Abstractions.Identity;
using CPMS.Application.DTOs.Accounts;
using CPMS.Shared.Models;

namespace CPMS.Application.Features.Accounts.Queries.GetAccounts;

public sealed class GetAccountsQueryHandler(IAccountRepository accounts)
{
    public async Task<PagedResult<AccountResponse>> HandleAsync(
        PagedRequest request,
        CancellationToken cancellationToken) =>
        await accounts.GetPagedAsync(request, cancellationToken);
}
