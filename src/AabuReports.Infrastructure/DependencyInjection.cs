using AabuReports.Infrastructure.ExtensionMethods;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AabuReports.Infrastructure;

public static class InfrastructureDependencyInjectionExtension
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddSqlServer(configuration)
            .AddIdentityCore()
            .AddRedis(configuration)
            .AddServices()
            .AddRepositories()
            .AddServiceDecorators()
            .AddBackGroundServices();

        return services;
    }
}
