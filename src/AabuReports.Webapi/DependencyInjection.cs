using AabuReports.Webapi.ExtensionMethods;

namespace AabuReports.Webapi;

public static class WebApiDependencyInjectionExtension
{
    public static IServiceCollection AddWebApi(this IServiceCollection services, IConfiguration configuration)
    {
        return services
            .AddApiServices()
            .AddRateLimiting()
            .AddRealTimeNotifications()
            .AddWebApiOptions(configuration)
            .AddCookieServices()
            .AddCorsPolicy(configuration)
            .AddJwtAuthentication();
    }
}
