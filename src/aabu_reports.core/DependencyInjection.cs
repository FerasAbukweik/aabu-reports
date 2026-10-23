using Microsoft.Extensions.DependencyInjection;

namespace aabu_reports.core;

public static class CoreDependencyInjectionExtension
{
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        return services;
    }
}