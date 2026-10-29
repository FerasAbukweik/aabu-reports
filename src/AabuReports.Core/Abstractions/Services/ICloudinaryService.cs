using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;

namespace AabuReports.Core.Abstractions.Services;

public interface ICloudinaryService
{
    Task<ImageUploadResult> Upload(IFormFile image);
    Task<DeletionResult> Delete(string publicId);

    /// <summary>
    /// deletes many images with as few Cloudinary requests as possible (100 images per request)
    /// </summary>
    /// <returns> true if every request succeeded (images that are already gone count as deleted) </returns>
    Task<bool> DeleteMany(IReadOnlyCollection<string> publicIds, CancellationToken cancellationToken = default);
}