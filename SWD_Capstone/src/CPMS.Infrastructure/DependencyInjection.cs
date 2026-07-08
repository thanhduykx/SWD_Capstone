using CPMS.Application.Abstractions.Cache;
using CPMS.Application.Abstractions.Identity;
using CPMS.Application.Abstractions.Messaging;
using CPMS.Application.Abstractions.Persistence;
using CPMS.Application.Abstractions.Security;
using CPMS.Infrastructure.Cache;
using CPMS.Infrastructure.Data;
using CPMS.Infrastructure.ExternalServices;
using CPMS.Infrastructure.Messaging;
using CPMS.Infrastructure.Repositories;
using CPMS.Infrastructure.UnitOfWork;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using StackExchange.Redis;

namespace CPMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();
        services.AddScoped<IAccountRepository, AccountRepository>();
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
