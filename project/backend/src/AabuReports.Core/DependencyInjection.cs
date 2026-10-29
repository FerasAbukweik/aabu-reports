using Microsoft.Extensions.DependencyInjection;

namespace AabuReports.Core;

public static class CoreDependencyInjectionExtensionMethod
{
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        return services;
    }
}