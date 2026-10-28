using AabuReports.Core.Abstractions.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AabuReports.Infrastructure.BackGroundServices;

public class RemoveExpiredRefreshTokens(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<RemoveExpiredRefreshTokens> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RemoveExpiredRefreshTokensMethod(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // a failed run (e.g. the DB is not reachable yet) must not stop the whole app, try again on the next run
                logger.LogError(ex, "{ServiceName} failed removing expired refresh tokens", nameof(RemoveExpiredRefreshTokens));
            }
            
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }

    private async Task RemoveExpiredRefreshTokensMethod(CancellationToken stoppingToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        
        // get RefreshTokenRepository
        var refreshTokenRepository = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();

        // remove expired refresh tokens
        await refreshTokenRepository.RemoveExpiredRefreshTokensAsync(stoppingToken);
    }
}

// add extension method for this BG service 
public static class AddRemoveExpiredRefreshTokensExtensionMethod
{
    public static IServiceCollection AddHostedRemoveExpiredRefreshTokens(this IServiceCollection services)
    {
        return services.AddHostedService<RemoveExpiredRefreshTokens>();
    }
}
