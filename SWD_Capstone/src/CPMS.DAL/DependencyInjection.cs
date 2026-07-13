using CPMS.BLL.Abstractions.Cache;
using CPMS.BLL.Abstractions.Identity;
using CPMS.BLL.Abstractions.Messaging;
using CPMS.BLL.Abstractions.Persistence;
using CPMS.BLL.Abstractions.Security;
using CPMS.DAL.Cache;
using CPMS.DAL.Data;
using CPMS.DAL.ExternalServices;
using CPMS.DAL.Messaging;
using CPMS.DAL.Repositories;
using CPMS.DAL.Repositories.Reviews;
using CPMS.BLL.Reviews.Registration;
using CPMS.BLL.Reviews.Scheduling;
using CPMS.BLL.Reviews.Sessions;
using CPMS.BLL.Reviews.Publication;
using CPMS.BLL.Reviews.Assessment;
using CPMS.BLL.Academic;
using CPMS.DAL.Repositories.Academic;
using CPMS.BLL.Accounts;
using CPMS.DAL.Repositories.Accounts;
using CPMS.BLL.Defense.Management;
using CPMS.DAL.Repositories.Defense;
using CPMS.BLL.Defense.Sessions;
using CPMS.BLL.Academic.Import;
using CPMS.DAL.UnitOfWork;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using StackExchange.Redis;

namespace CPMS.DAL;

public static class DependencyInjection
{
    public static IServiceCollection AddDALServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IReviewRegistrationStore, ReviewRegistrationStore>();
        services.AddScoped<IReviewRoundStore, ReviewRoundStore>();
        services.AddScoped<IReviewSchedulingBoardStore, ReviewSchedulingBoardStore>();
        services.AddScoped<IReviewAssignmentStore, ReviewAssignmentStore>();
        services.AddScoped<IReviewSessionStore, ReviewSessionStore>();
        services.AddScoped<IReviewPublicationStore, ReviewPublicationStore>();
        services.AddScoped<IReviewAttendanceStore, ReviewAttendanceStore>();
        services.AddScoped<IReviewSubmissionStore, ReviewSubmissionStore>();
        services.AddScoped<ISemesterStore, SemesterStore>();
        services.AddScoped<IAuthenticationStore, AuthenticationStore>();
        services.AddScoped<IAccountAdministrationStore, AccountAdministrationStore>();
        services.AddScoped<IAccountWelcomeNotificationSender, AccountWelcomeNotificationSender>();
        services.AddScoped<IDefenseManagementStore, DefenseManagementStore>();
        services.AddScoped<IDefenseSessionStore, DefenseSessionStore>();
        services.AddScoped<IGroupImportStore, GroupImportStore>();
        services.AddScoped<IGroupWorkbookParser, ExcelGroupWorkbookParser>();
        services.AddScoped<DatabaseLifecycleService>();
        services.AddScoped<IDatabaseLifecycleService>(provider => provider.GetRequiredService<DatabaseLifecycleService>());
        services.AddScoped<CPMS.BLL.Testing.ITestDataResetService>(provider =>
            provider.GetRequiredService<DatabaseLifecycleService>());
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped(typeof(IRepository<,>), typeof(GenericRepository<,>));
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();

        var redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
            services.AddSingleton<ICacheService, RedisCacheService>();
        }

        var rabbitMqConnection = configuration.GetConnectionString("RabbitMQ");
        if (!string.IsNullOrWhiteSpace(rabbitMqConnection))
        {
            services.AddSingleton<IConnection>(_ =>
            {
                var factory = new ConnectionFactory { Uri = new Uri(rabbitMqConnection) };
                return factory.CreateConnection();
            });
            services.AddSingleton<IMessageBus, RabbitMqMessageBus>();
        }

        return services;
    }
}
