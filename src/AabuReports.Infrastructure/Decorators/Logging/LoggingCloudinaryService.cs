using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Results;
using AabuReports.Infrastructure.Services;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AabuReports.Infrastructure.Decorators.Logging;

/// <summary>
/// logs the uploads and deletes of <see cref="CloudinaryService"/>.
/// Cloudinary reports its failures inside the returned objects (not as exceptions), so they are read here
/// </summary>
public class LoggingCloudinaryService(
    ICloudinaryService innerService,
    ILogger<CloudinaryService> logger) : ICloudinaryService
{
    private const string Name = nameof(CloudinaryService);

    public async Task<ImageUploadResult> Upload(IFormFile image)
    {
        var result = await innerService.Upload(image);

        if (result.Error != null)
            logger.LogError("{ServiceName}.{MethodName} failed uploading image\nErrors: {errors}",
                Name, nameof(Upload), result.Error.Message);
        else if (!string.IsNullOrEmpty(result.PublicId)) // an empty file is skipped by the service, nothing was uploaded
            logger.LogInformation("{ServiceName}.{MethodName} image was uploaded with publicId of {publicId}",
                Name, nameof(Upload), result.PublicId);

        return result;
    }

    public async Task<DeletionResult> Delete(string publicId)
    {
        var result = await innerService.Delete(publicId);

        if (result.Error != null)
            logger.LogError("{ServiceName}.{MethodName} failed deleting image\nErrors: {errors}",
                Name, nameof(Delete), result.Error.Message);
        else
            logger.LogInformation("{ServiceName}.{MethodName} image with public id of {publicId} was deleted",
                Name, nameof(Delete), publicId);

        return result;
    }

    public async Task<Result> DeleteMany(IReadOnlyCollection<string> publicIds, CancellationToken cancellationToken = default)
    {
        var result = await innerService.DeleteMany(publicIds, cancellationToken);

        if (result.IsSuccess)
            logger.LogInformation("{ServiceName}.{MethodName} deleted {count} images",
                Name, nameof(DeleteMany), publicIds.Distinct().Count());
        else
            logger.LogError("{ServiceName}.{MethodName} failed\nErrors: {errors}",
                Name, nameof(DeleteMany), result.ErrorMessage);

        return result;
    }
}
