using System.Net;
using AabuReports.Core.Abstractions.Repositories;
using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.Images;
using AabuReports.Core.Entities;
using AabuReports.Core.Results;
using Microsoft.AspNetCore.Http;

namespace AabuReports.Infrastructure.Services;

public class ImageService(
    IImageRepository imageRepository,
    ICloudinaryService cloudinaryService) : IImageService
{
    private const string ImageNotFoundMessage = "Image not found";
    private const string NoImagesMessage = "No images were sent";
    private const string NotAnImageMessage = "Only non-empty image files are allowed";
    private const string CloudinaryDeleteFailedMessage = "Failed to delete the images from the image storage";
    private const string CloudinaryUploadFailedMessage = "Failed to upload the images to the image storage";

    public async Task<Result<IReadOnlyList<ImageDto>>> UploadForReportAsync(Guid reportId, IReadOnlyList<IFormFile> images, CancellationToken cancellationToken = default)
    {
        // all the checks come first, nothing is uploaded until we know the request is valid
        if (images.Count == 0)
            return Result<IReadOnlyList<ImageDto>>.Failure(NoImagesMessage);

        if (images.Any(i => i.Length == 0 || !i.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)))
            return Result<IReadOnlyList<ImageDto>>.Failure(NotAnImageMessage);

        // last step: upload to Cloudinary (saving to the DB needs the url and the public id it returns)
        var uploads = await Task.WhenAll(images.Select(cloudinaryService.Upload));

        var uploadedPublicIds = uploads
            .Where(u => u.Error == null && !string.IsNullOrEmpty(u.PublicId))
            .Select(u => u.PublicId)
            .ToList();

        if (uploadedPublicIds.Count != uploads.Length)
        {
            // one of the uploads failed, so don't keep the ones that succeeded
            var rollbackFailure = await RollbackUploadsAsync(uploadedPublicIds);

            return Result<IReadOnlyList<ImageDto>>.Failure(CloudinaryUploadFailedMessage, HttpStatusCode.InternalServerError, rollbackFailure);
        }

        var newImages = uploads.Select(u => new Image
        {
            Url = u.SecureUrl.ToString(),
            PublicId = u.PublicId,
            ReportId = reportId
        }).ToList();

        imageRepository.AddRange(newImages);

        if (!await imageRepository.SaveChangesAsync(cancellationToken))
        {
            var rollbackFailure = await RollbackUploadsAsync(uploadedPublicIds);
            return Result<IReadOnlyList<ImageDto>>.Failure("no changes where applied to the DB", HttpStatusCode.InternalServerError, rollbackFailure);
        }
        
        return Result<IReadOnlyList<ImageDto>>.Success(
            newImages.Select(i => i.ToDto()).ToList(),
            HttpStatusCode.Created);
    }

    public async Task<Result> DeleteByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var image = await imageRepository.GetTrackedByIdAsync(id, cancellationToken);
        if (image == null)
            return Result.Failure(ImageNotFoundMessage, HttpStatusCode.NotFound);

        // the image is only detached from its report, the RemoveOrphanedImages background service
        // removes it from Cloudinary and from the DB (same as when its report is removed)
        image.ReportId = null;

        // saving returns false when the image was already detached, that is not an error
        await imageRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(HttpStatusCode.NoContent);
    }

    public async Task<Result> DeleteOrphanedAsync(CancellationToken cancellationToken = default)
    {
        // Cloudinary deletes at most 100 images per request, so work in batches of 100
        const int batchSize = 100;

        while (true)
        {
            // ReportId is set to null by the DB when the report is removed, so no extra query is needed to find the orphans
            var orphans = await imageRepository.FilterAsync(i => i.ReportId == null, batchSize, cancellationToken);
            if (orphans.Count == 0) return Result.Success();

            // Cloudinary first: if it fails nothing is removed from the DB, so the same orphans are retried on the next run
            var deleted = await cloudinaryService.DeleteMany(orphans.Select(i => i.PublicId).ToList(), cancellationToken);
            if (!deleted.IsSuccess)
                return Result.Failure(CloudinaryDeleteFailedMessage, HttpStatusCode.InternalServerError, deleted.ErrorMessage);

            // same ids that were sent to Cloudinary, so an image that became an orphan in the meantime is not removed from the DB only
            var ids = orphans.Select(i => i.Id).ToList();
            await imageRepository.ExecuteRemoveAsync(i => ids.Contains(i.Id), cancellationToken);

            if (orphans.Count < batchSize) return Result.Success();
        }
    }


    // private

    /// <returns> null if the rollback worked, otherwise the details for the logs (the public ids that stayed on Cloudinary) </returns>
    private async Task<string?> RollbackUploadsAsync(IReadOnlyCollection<string> publicIds)
    {
        if (publicIds.Count == 0) return null;

        // CancellationToken.None: the rollback must run even if the request was cancelled
        var deleted = await cloudinaryService.DeleteMany(publicIds, CancellationToken.None);

        return deleted.IsSuccess
            ? null
            : $"failed rolling back uploaded images, public ids: {string.Join(", ", publicIds)}. Errors: {deleted.ErrorMessage}";
    }
}
