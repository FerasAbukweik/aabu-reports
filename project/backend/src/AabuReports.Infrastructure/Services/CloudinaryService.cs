using AabuReports.Core.Abstractions.Services;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AabuReports.Infrastructure.Services;

public class CloudinaryService : ICloudinaryService
{
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<CloudinaryService> _logger;
    
    
    public CloudinaryService(
        IConfiguration configuration,
        ILogger<CloudinaryService> logger
        )
    {
        _logger = logger;
        
        var account = new Account(
            configuration["CloudinarySettings:CloudName"], 
            configuration["CloudinarySettings:ApiKey"],
            configuration["CloudinarySettings:ApiSecret"]);
        
        _cloudinary = new Cloudinary(account);
    }
    
    public async Task<ImageUploadResult> Upload(IFormFile image)
    {
        if (image.Length == 0) return new ImageUploadResult();
        
        await using var stream = image.OpenReadStream();
        var uploadParams = new ImageUploadParams()
        {
            File = new FileDescription(image.FileName, stream),
            // Transformation = new Transformation().Height(500).Width(500).Crop("fill"),
            Folder = "aabu_reports"
        };
        
        var result = await _cloudinary.UploadAsync(uploadParams);

        if (result.Error != null)
        {
            _logger.LogError("{serviceName}.{methodName} failed uploading image\nErrors: {errors}",
                nameof(CloudinaryService), nameof(Upload), result.Error.Message);
        }
        else
        {
            _logger.LogError("{serviceName}.{methodName} image was uploaded with publicId of {publicId}",
                nameof(CloudinaryService), nameof(Upload), result.PublicId);
        }

        return result;
    }

    public async Task<DeletionResult> Delete(string publicId)
    {
        var deletionParams = new DeletionParams(publicId);

        var result = await _cloudinary.DestroyAsync(deletionParams);

        if (result.Error != null)
        {
            _logger.LogError("{serviceName}.{methodName} failed deleting image\nErrors: {errors}",
                nameof(CloudinaryService), nameof(Delete), result.Error.Message);
        }
        else
        {
            _logger.LogError("{serviceName}.{methodName} image with public id of {publicId} was deleted",
                nameof(CloudinaryService), nameof(Delete), publicId);
        }
        
        return result;
    }

    public async Task<bool> DeleteMany(IReadOnlyCollection<string> publicIds, CancellationToken cancellationToken = default)
    {
        // Cloudinary accepts at most 100 public ids per delete request
        const int batchSize = 100;

        var allDeleted = true;

        foreach (var batch in publicIds.Distinct().Chunk(batchSize))
        {
            var result = await _cloudinary.DeleteResourcesAsync(
                new DelResParams
                {
                    PublicIds = batch.ToList(),
                    ResourceType = ResourceType.Image
                },
                cancellationToken);

            if (result.Error != null)
            {
                allDeleted = false;
                _logger.LogError("{serviceName}.{methodName} failed deleting {count} images\nErrors: {errors}",
                    nameof(CloudinaryService), nameof(DeleteMany), batch.Length, result.Error.Message);
            }
            else
            {
                _logger.LogInformation("{serviceName}.{methodName} deleted {count} images",
                    nameof(CloudinaryService), nameof(DeleteMany), batch.Length);
            }
        }

        return allDeleted;
    }
}