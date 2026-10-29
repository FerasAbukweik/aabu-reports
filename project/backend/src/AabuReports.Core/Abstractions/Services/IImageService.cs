using AabuReports.Core.Dtos.Images;
using AabuReports.Core.Results;
using Microsoft.AspNetCore.Http;

namespace AabuReports.Core.Abstractions.Services;

public interface IImageService
{
    /// <summary>
    /// uploads the images to Cloudinary and attaches them to a report,
    /// the upload is the last step so nothing is uploaded if a check fails before it
    /// </summary>
    /// <returns> result object with the created images, 400 if a file is empty or not an image, 404 if the report doesnt exist </returns>
    Task<Result<IReadOnlyList<ImageDto>>> UploadForReportAsync(Guid reportId, IReadOnlyList<IFormFile> images, CancellationToken cancellationToken = default);

    /// <summary>
    /// removes one image: detaches it from its report (ReportId becomes null),
    /// and the orphaned images background service deletes it from Cloudinary and from the DB later
    /// </summary>
    /// <returns> result object, 404 if it doesnt exist </returns>
    Task<Result> DeleteByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// deletes the orphaned images (their report was removed, so ReportId is null) from Cloudinary and from the DB,
    /// in batches of 100 until none are left or a batch fails (the failed batch stays in the DB and is retried on the next run)
    /// </summary>
    /// <returns> result object, the error is the failure of the first batch that could not be deleted from Cloudinary </returns>
    Task<Result> DeleteOrphanedAsync(CancellationToken cancellationToken = default);
}
