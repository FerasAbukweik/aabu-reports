using System.Net;
using AabuReports.Core.Abstractions.Services;
using Result = AabuReports.Core.Results.Result;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace AabuReports.Infrastructure.Services;

public class CloudinaryService : ICloudinaryService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryService(IConfiguration configuration)
    {
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
        
        return await _cloudinary.UploadAsync(uploadParams);
    }

    public async Task<DeletionResult> Delete(string publicId)
    {
        var deletionParams = new DeletionParams(publicId);

        return await _cloudinary.DestroyAsync(deletionParams);
    }

    public async Task<Result> DeleteMany(IReadOnlyCollection<string> publicIds, CancellationToken cancellationToken = default)
    {
        // Cloudinary accepts at most 100 public ids per delete request
        const int batchSize = 100;

        var errors = new List<string>();

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
                errors.Add($"failed deleting {batch.Length} images: {result.Error.Message}");
        }

        // the error is internal (Cloudinary's messages), the callers must not send it to the client
        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(string.Join("; ", errors), HttpStatusCode.InternalServerError);
    }
}
