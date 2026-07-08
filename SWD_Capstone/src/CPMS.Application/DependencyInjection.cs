using CPMS.Application.Abstractions.Identity;
using CPMS.Application.Features.Accounts.Commands.CreateAccount;
using CPMS.Application.Features.Accounts.Queries.GetAccounts;
using CPMS.Core.Services;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CPMS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAccountUsernameGenerator, DefaultAccountUsernameGenerator>();
        services.AddScoped<CreateAccountCommandHandler>();
        services.AddScoped<GetAccountsQueryHandler>();
        services.AddValidatorsFromAssemblyContaining<CreateAccountCommandValidator>();
        return services;
    }
}
