using System.Linq.Expressions;
using System.Net;
using AabuReports.Core.Abstractions.Repositories;
using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Entities;
using AabuReports.Infrastructure.Services;
using CloudinaryDotNet.Actions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit.Abstractions;

namespace AabuReports.Services.UnitTests;

public class ImageServiceTests : ServiceTestBase
{
    private readonly IImageService _imageService;
    private readonly Mock<IImageRepository> _imageRepositoryMock;
    private readonly Mock<ICloudinaryService> _cloudinaryMock;

    public ImageServiceTests(ITestOutputHelper output) : base(output)
    {
        _imageRepositoryMock = new Mock<IImageRepository>();
        _cloudinaryMock = new Mock<ICloudinaryService>();

        _imageService = new ImageService(
            _imageRepositoryMock.Object,
            _cloudinaryMock.Object,
            NullLogger<ImageService>.Instance);
    }

    private static ImageUploadResult SuccessfulUpload(string publicId) => new()
    {
        PublicId = publicId,
        SecureUrl = new Uri($"https://cdn.test/{publicId}.png")
    };

    private static ImageUploadResult FailedUpload() => new() { Error = new Error { Message = "upload failed" } };

    private Mock<IFormFile> SetupUpload(ImageUploadResult upload)
    {
        var file = CreateFileMock();
        _cloudinaryMock.Setup(c => c.Upload(file.Object)).ReturnsAsync(upload);
        return file;
    }

    #region UploadForReportAsync

    [Fact]
    public async Task UploadForReportAsync_ShouldReturnBadRequest_WhenNoImagesWereSent()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var images = new List<IFormFile>();

        // Act
        var result = await _imageService.UploadForReportAsync(reportId, images);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.ErrorMessage.Should().Be("No images were sent");
    }

    [Theory]
    [InlineData("application/pdf", 1024)]
    [InlineData("text/plain", 1024)]
    [InlineData("image/png", 0)]
    public async Task UploadForReportAsync_ShouldReturnBadRequestAndUploadNothing_WhenAFileIsNotANonEmptyImage(string contentType, long length)
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var images = new List<IFormFile>
        {
            CreateFileMock().Object,
            CreateFileMock(contentType, length).Object
        };
        LogArrange("files", new { contentType, length });

        // Act
        var result = await _imageService.UploadForReportAsync(reportId, images);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.ErrorMessage.Should().Be("Only non-empty image files are allowed");

        _cloudinaryMock.Verify(c => c.Upload(It.IsAny<IFormFile>()), Times.Never);
    }

    [Fact]
    public async Task UploadForReportAsync_ShouldRollbackSucceededUploads_WhenOneUploadFails()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var good = SetupUpload(SuccessfulUpload("pub-1"));
        var bad = SetupUpload(FailedUpload());
        var images = new List<IFormFile> { good.Object, bad.Object };

        _cloudinaryMock.Setup(c => c.DeleteMany(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _imageService.UploadForReportAsync(reportId, images);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        result.ErrorMessage.Should().Be("Failed to upload the images to the image storage");

        // only the image that did upload is removed from Cloudinary, and the DB is never touched
        _cloudinaryMock.Verify(
            c => c.DeleteMany(It.Is<IReadOnlyCollection<string>>(ids => ids.SequenceEqual(new[] { "pub-1" })), CancellationToken.None),
            Times.Once);
        _imageRepositoryMock.Verify(r => r.AddRange(It.IsAny<IEnumerable<Image>>()), Times.Never);
    }

    [Fact]
    public async Task UploadForReportAsync_ShouldNotCallDeleteMany_WhenEveryUploadFails()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var images = new List<IFormFile> { SetupUpload(FailedUpload()).Object, SetupUpload(FailedUpload()).Object };

        // Act
        var result = await _imageService.UploadForReportAsync(reportId, images);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        _cloudinaryMock.Verify(
            c => c.DeleteMany(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadForReportAsync_ShouldRollbackUploads_WhenNothingWasSavedToTheDb()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var images = new List<IFormFile> { SetupUpload(SuccessfulUpload("pub-1")).Object };

        _imageRepositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _cloudinaryMock.Setup(c => c.DeleteMany(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _imageService.UploadForReportAsync(reportId, images);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        result.ErrorMessage.Should().Be("no changes where applied to the DB");

        _cloudinaryMock.Verify(
            c => c.DeleteMany(It.Is<IReadOnlyCollection<string>>(ids => ids.SequenceEqual(new[] { "pub-1" })), CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task UploadForReportAsync_ShouldSaveImagesAndReturnCreated_WhenAllUploadsSucceed()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var images = new List<IFormFile>
        {
            SetupUpload(SuccessfulUpload("pub-1")).Object,
            SetupUpload(SuccessfulUpload("pub-2")).Object
        };
        LogArrange(nameof(reportId), reportId);

        List<Image>? added = null;
        _imageRepositoryMock.Setup(r => r.AddRange(It.IsAny<IEnumerable<Image>>()))
            .Callback<IEnumerable<Image>>(i => added = i.ToList());
        _imageRepositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await _imageService.UploadForReportAsync(reportId, images);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Value.Should().HaveCount(2);
        result.Value!.Select(i => i.Url).Should().Equal("https://cdn.test/pub-1.png", "https://cdn.test/pub-2.png");

        added.Should().NotBeNull();
        added!.Select(i => i.PublicId).Should().Equal("pub-1", "pub-2");
        added.Should().OnlyContain(i => i.ReportId == reportId);

        _cloudinaryMock.Verify(
            c => c.DeleteMany(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region DeleteByIdAsync

    [Fact]
    public async Task DeleteByIdAsync_ShouldReturnNotFound_WhenImageDoesNotExist()
    {
        // Arrange
        var id = Guid.NewGuid();
        LogArrange(nameof(id), id);

        _imageRepositoryMock.Setup(r => r.GetTrackedByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Image?)null);

        // Act
        var result = await _imageService.DeleteByIdAsync(id);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
        result.ErrorMessage.Should().Be("Image not found");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)] // false = the image was already detached, that is not an error
    public async Task DeleteByIdAsync_ShouldDetachImageFromItsReportAndReturnNoContent(bool saved)
    {
        // Arrange
        var image = new Image { Url = "https://cdn.test/a.png", PublicId = "pub-1", ReportId = Guid.NewGuid() };
        LogArrange(nameof(image), image);

        _imageRepositoryMock.Setup(r => r.GetTrackedByIdAsync(image.Id, It.IsAny<CancellationToken>())).ReturnsAsync(image);
        _imageRepositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(saved);

        // Act
        var result = await _imageService.DeleteByIdAsync(image.Id);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.NoContent);
        image.ReportId.Should().BeNull();

        // Cloudinary is only touched later by the orphan cleanup
        _cloudinaryMock.Verify(c => c.Delete(It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region DeleteOrphanedAsync

    private void SetupOrphanBatches(params IReadOnlyList<Image>[] batches)
    {
        var sequence = _imageRepositoryMock.SetupSequence(r => r.FilterAsync(
            It.IsAny<Expression<Func<Image, bool>>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()));

        foreach (var batch in batches)
            sequence = sequence.ReturnsAsync(batch);
    }

    private static List<Image> CreateOrphans(int count) =>
        Enumerable.Range(0, count)
            .Select(i => new Image { Url = $"https://cdn.test/{i}.png", PublicId = $"pub-{i}" })
            .ToList();

    [Fact]
    public async Task DeleteOrphanedAsync_ShouldSucceedWithoutTouchingCloudinary_WhenThereAreNoOrphans()
    {
        // Arrange
        SetupOrphanBatches(new List<Image>());

        // Act
        var result = await _imageService.DeleteOrphanedAsync();
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _cloudinaryMock.Verify(
            c => c.DeleteMany(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteOrphanedAsync_ShouldNotRemoveFromDb_WhenCloudinaryFails()
    {
        // Arrange
        var orphans = CreateOrphans(3);
        LogArrange(nameof(orphans), orphans);
        SetupOrphanBatches(orphans);

        _cloudinaryMock.Setup(c => c.DeleteMany(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _imageService.DeleteOrphanedAsync();
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        result.ErrorMessage.Should().Be("Failed to delete the images from the image storage");

        _imageRepositoryMock.Verify(
            r => r.ExecuteRemoveAsync(It.IsAny<Expression<Func<Image, bool>>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteOrphanedAsync_ShouldDeleteFromCloudinaryAndDb_WhenOneBatchIsEnough()
    {
        // Arrange
        var orphans = CreateOrphans(3);
        LogArrange(nameof(orphans), orphans);
        SetupOrphanBatches(orphans);

        _cloudinaryMock.Setup(c => c.DeleteMany(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _imageRepositoryMock.Setup(r => r.ExecuteRemoveAsync(It.IsAny<Expression<Func<Image, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        // Act
        var result = await _imageService.DeleteOrphanedAsync();
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _cloudinaryMock.Verify(
            c => c.DeleteMany(
                It.Is<IReadOnlyCollection<string>>(ids => ids.SequenceEqual(new[] { "pub-0", "pub-1", "pub-2" })),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _imageRepositoryMock.Verify(
            r => r.ExecuteRemoveAsync(It.IsAny<Expression<Func<Image, bool>>>(), It.IsAny<CancellationToken>()), Times.Once);

        // fewer than a full batch, so there is no second query
        _imageRepositoryMock.Verify(
            r => r.FilterAsync(It.IsAny<Expression<Func<Image, bool>>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteOrphanedAsync_ShouldKeepGoing_WhenABatchIsFull()
    {
        // Arrange: a full batch of 100, then an empty one
        var firstBatch = CreateOrphans(100);
        SetupOrphanBatches(firstBatch, new List<Image>());

        _cloudinaryMock.Setup(c => c.DeleteMany(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _imageRepositoryMock.Setup(r => r.ExecuteRemoveAsync(It.IsAny<Expression<Func<Image, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        // Act
        var result = await _imageService.DeleteOrphanedAsync();
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _imageRepositoryMock.Verify(
            r => r.FilterAsync(It.IsAny<Expression<Func<Image, bool>>>(), 100, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        _cloudinaryMock.Verify(
            c => c.DeleteMany(It.Is<IReadOnlyCollection<string>>(ids => ids.Count == 100), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}