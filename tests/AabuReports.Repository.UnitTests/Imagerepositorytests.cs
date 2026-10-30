using AabuReports.Core.Entities;
using AabuReports.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace AabuReports.Repository.UnitTests;

public class ImageRepositoryTests : RepositoryTestBase
{
    private readonly ImageRepository _repository;

    public ImageRepositoryTests(ITestOutputHelper output) : base(output)
    {
        _repository = new ImageRepository(_dbContext);
    }

    [Fact]
    public async Task AddRange_ShouldPersistAllImages_WhenChangesAreSaved()
    {
        // Arrange
        var user = await SeedUserAsync();
        var location = await SeedLocationAsync();
        var report = await SeedReportAsync(user.Id, location.Id);
        var images = new List<Image>
        {
            new() { Url = "https://cdn.test/1.png", PublicId = "pub-1", ReportId = report.Id },
            new() { Url = "https://cdn.test/2.png", PublicId = "pub-2", ReportId = report.Id }
        };
        LogArrange(nameof(images), images);

        // Act
        _repository.AddRange(images);
        var saved = await _repository.SaveChangesAsync();
        LogAct(saved);

        // Assert
        saved.Should().BeTrue();

        _dbContext.ChangeTracker.Clear();
        (await _dbContext.Images.Where(i => i.ReportId == report.Id).Select(i => i.PublicId).ToListAsync())
            .Should().BeEquivalentTo("pub-1", "pub-2");
    }

    [Fact]
    public async Task AddRange_ShouldThrow_WhenThePublicIdAlreadyExists()
    {
        // Arrange
        await SeedImageAsync(null, "pub-1");
        _repository.AddRange(new[] { new Image { Url = "https://cdn.test/x.png", PublicId = "pub-1" } });

        LogArrange("existing public id", "pub-1");

        // Act
        var act = () => _repository.SaveChangesAsync();
        LogAct("expecting the save to throw");

        // Assert: the public id has a unique index
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task FilterAsync_ShouldReturnOnlyMatchingImagesAndNotTrackThem()
    {
        // Arrange
        var user = await SeedUserAsync();
        var location = await SeedLocationAsync();
        var report = await SeedReportAsync(user.Id, location.Id);
        var orphan1 = await SeedImageAsync(null);
        var orphan2 = await SeedImageAsync(null);
        await SeedImageAsync(report.Id);

        LogArrange("arranged data", new { user, location, report, orphan1, orphan2 });

        // Act
        var result = await _repository.FilterAsync(i => i.ReportId == null);
        LogAct(result);

        // Assert
        result.Select(i => i.Id).Should().BeEquivalentTo(new[] { orphan1.Id, orphan2.Id });
        _dbContext.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task FilterAsync_ShouldReturnAtMostTake_WhenTakeIsGiven()
    {
        // Arrange
        for (var i = 0; i < 5; i++) await SeedImageAsync(null);

        LogArrange("orphan images seeded", 5);

        // Act
        var result = await _repository.FilterAsync(i => i.ReportId == null, 3);
        LogAct(result);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task ExecuteRemoveAsync_ShouldDeleteOnlyMatchingImagesWithoutSaving()
    {
        // Arrange
        var keep = await SeedImageAsync(null);
        var remove1 = await SeedImageAsync(null);
        var remove2 = await SeedImageAsync(null);
        var ids = new List<Guid> { remove1.Id, remove2.Id };
        LogArrange(nameof(ids), ids);

        // Act
        var removed = await _repository.ExecuteRemoveAsync(i => ids.Contains(i.Id));
        LogAct(removed);

        // Assert
        removed.Should().Be(2);

        _dbContext.ChangeTracker.Clear();
        (await _dbContext.Images.Select(i => i.Id).ToListAsync()).Should().BeEquivalentTo(new[] { keep.Id });
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnTheImage_OrNullWhenMissing()
    {
        // Arrange
        var image = await SeedImageAsync(null);

        LogArrange(nameof(image), image);

        // Act
        var found = await _repository.GetByIdAsync(image.Id);
        var missing = await _repository.GetByIdAsync(Guid.NewGuid());
        LogAct(new { found, missing });

        // Assert
        found.Should().NotBeNull();
        found!.PublicId.Should().Be(image.PublicId);
        missing.Should().BeNull();
    }

    [Fact]
    public async Task GetTrackedByIdAsync_ShouldReturnATrackedImage_SoChangesAreSaved()
    {
        // Arrange
        var user = await SeedUserAsync();
        var location = await SeedLocationAsync();
        var report = await SeedReportAsync(user.Id, location.Id);
        var image = await SeedImageAsync(report.Id);
        LogArrange(nameof(image), image);

        // Act: this is how ImageService.DeleteByIdAsync detaches an image from its report
        var tracked = await _repository.GetTrackedByIdAsync(image.Id);
        tracked!.ReportId = null;
        var saved = await _repository.SaveChangesAsync();
        LogAct(saved);

        // Assert
        saved.Should().BeTrue();

        _dbContext.ChangeTracker.Clear();
        (await _dbContext.Images.SingleAsync(i => i.Id == image.Id)).ReportId.Should().BeNull();
    }

    [Fact]
    public async Task GetTrackedByIdAsync_ShouldReturnNull_WhenImageDoesNotExist()
    {
        var id = Guid.NewGuid();
        LogArrange(nameof(id), id);

        // Act
        var result = await _repository.GetTrackedByIdAsync(id);
        LogAct(result);

        // Assert
        result.Should().BeNull();
    }
}