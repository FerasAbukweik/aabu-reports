using AabuReports.Core.Abstractions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AabuReports.Infrastructure.BackGroundServices;

/// <summary>
/// every 24 hours removes the images whose report was removed (ReportId is null) from Cloudinary and from the DB
/// </summary>
public class RemoveOrphanedImages(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<RemoveOrphanedImages> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RemoveOrphanedImagesMethod(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // a failed run (e.g. the DB is not reachable yet) must not stop the whole app, try again on the next run
                logger.LogError(ex, "{ServiceName} failed removing orphaned images", nameof(RemoveOrphanedImages));
            }

            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }

    private async Task RemoveOrphanedImagesMethod(CancellationToken stoppingToken)
    {
        using var scope = serviceScopeFactory.CreateScope();

        var imageService = scope.ServiceProvider.GetRequiredService<IImageService>();

        var result = await imageService.DeleteOrphanedAsync(stoppingToken);

        if (!result.IsSuccess)
        {
            logger.LogError("{ServiceName} could not remove all orphaned images: {error}",
                nameof(RemoveOrphanedImages), result.ErrorMessage);
        }
    }
}

// add extension method for this BG service
public static class AddRemoveOrphanedImagesExtensionMethod
{
    public static IServiceCollection AddHostedRemoveOrphanedImages(this IServiceCollection services)
    {
        return services.AddHostedService<RemoveOrphanedImages>();
    }
}
