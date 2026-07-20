using CPMS.BLL.Abstractions.Identity;
using CPMS.BLL.Features.Accounts.Commands.CreateAccount;
using CPMS.BLL.Features.Accounts.Queries.GetAccounts;
using CPMS.BLL.Services;
using CPMS.BLL.Reviews.Registration;
using CPMS.BLL.Reviews.Scheduling;
using CPMS.BLL.Reviews.Sessions;
using CPMS.BLL.Reviews.Publication;
using CPMS.BLL.Reviews.Assessment;
using CPMS.BLL.Academic;
using CPMS.BLL.Accounts;
using CPMS.BLL.Defense.Management;
using CPMS.BLL.Defense.Sessions;
using CPMS.BLL.Academic.Import;
using CPMS.BLL.AI.ProjectSuggestions;
using CPMS.BLL.Documents;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CPMS.BLL;

public static class DependencyInjection
{
    public static IServiceCollection AddBLLServices(this IServiceCollection services)
    {
        services.AddScoped<IAccountUsernameGenerator, DefaultAccountUsernameGenerator>();
        services.AddScoped<CreateAccountCommandHandler>();
        services.AddScoped<GetAccountsQueryHandler>();
        services.AddValidatorsFromAssemblyContaining<CreateAccountCommandValidator>();
        services.AddScoped<IReviewRegistrationService, ReviewRegistrationService>();
        services.AddScoped<IReviewRoundService, ReviewRoundService>();
        services.AddScoped<IReviewSchedulingBoardService, ReviewSchedulingBoardService>();
        services.AddScoped<IReviewAssignmentService, ReviewAssignmentService>();
        services.AddScoped<IReviewSessionService, ReviewSessionService>();
        services.AddScoped<IReviewPublicationService, ReviewPublicationService>();
        services.AddScoped<IReviewAttendanceService, ReviewAttendanceService>();
        services.AddScoped<IReviewSubmissionService, ReviewSubmissionService>();
        services.AddScoped<ISemesterService, SemesterService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IAccountAdministrationService, AccountAdministrationService>();
        services.AddScoped<IDefenseManagementService, DefenseManagementService>();
        services.AddScoped<IDefenseSessionService, DefenseSessionService>();
        services.AddScoped<IGroupImportService, GroupImportService>();
        services.AddScoped<IProjectSuggestionService, ProjectSuggestionService>();
        services.AddScoped<IProjectDocumentService, ProjectDocumentService>();
        return services;
    }
}
