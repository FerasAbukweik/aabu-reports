using AabuReports.Core.Dtos.LazyLoading;
using AabuReports.Core.Dtos.Reports;
using AabuReports.Core.Entities;
using AabuReports.Core.Enums;
using AabuReports.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace AabuReports.Repository.UnitTests;

public class ReportRepositoryTests : RepositoryTestBase
{
    private readonly ReportRepository _repository;

    public ReportRepositoryTests(ITestOutputHelper output) : base(output)
    {
        _repository = new ReportRepository(_dbContext);
    }

    private static LazyLoadingDto Page(int skip = 0, int take = 50) => new() { Skip = skip, Take = take };

    #region AddReport

    [Fact]
    public async Task AddReport_ShouldPersistTheReport_WhenChangesAreSaved()
    {
        // Arrange
        var user = await SeedUserAsync();
        var location = await SeedLocationAsync();
        var report = new Report
        {
            ReportDate = DateTime.UtcNow,
            Category = ReportCategory.Plumbing,
            State = ReportStateEnum.Pending,
            Severity = ReportSeverity.Large,
            Description = "Leaking pipe",
            LocationId = location.Id,
            UserId = user.Id
        };
        LogArrange(nameof(report), report);

        // Act
        _repository.AddReport(report);
        var saved = await _repository.SaveChangesAsync();
        LogAct(saved);

        // Assert
        saved.Should().BeTrue();

        _dbContext.ChangeTracker.Clear();
        var stored = await _dbContext.Reports.SingleAsync(r => r.Id == report.Id);
        stored.Description.Should().Be("Leaking pipe");
        stored.Category.Should().Be(ReportCategory.Plumbing);
        stored.State.Should().Be(ReportStateEnum.Pending);
        stored.Rate.Should().BeNull();
    }

    #endregion

    #region GetById / GetTrackedById / GetOwnerId

    [Fact]
    public async Task GetByIdAsync_ShouldReturnTheReportWithImagesAndLocation_WithoutTracking()
    {
        // Arrange
        var user = await SeedUserAsync();
        var location = await SeedLocationAsync("Library");
        var report = await SeedReportAsync(user.Id, location.Id);
        await SeedImageAsync(report.Id);
        await SeedImageAsync(report.Id);

        LogArrange("arranged data", new { user, location, report });

        // Act
        var result = await _repository.GetByIdAsync(report.Id);
        LogAct(result);

        // Assert
        result.Should().NotBeNull();
        result!.Images.Should().HaveCount(2);
        result.Location!.Title.Should().Be("Library");
        _dbContext.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenReportDoesNotExist()
    {
        var id = Guid.NewGuid();
        LogArrange(nameof(id), id);

        // Act
        var result = await _repository.GetByIdAsync(id);
        LogAct(result);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTrackedByIdAsync_ShouldReturnATrackedReport_SoChangesAreSaved()
    {
        // Arrange
        var user = await SeedUserAsync();
        var location = await SeedLocationAsync();
        var report = await SeedReportAsync(user.Id, location.Id, ReportStateEnum.Fixed);

        LogArrange("arranged data", new { user, location, report });

        // Act: this is how ReportService.RateAsync saves a rate
        var tracked = await _repository.GetTrackedByIdAsync(report.Id);
        tracked!.TryRate(5).Should().BeTrue();
        var saved = await _repository.SaveChangesAsync();
        LogAct(saved);

        // Assert
        saved.Should().BeTrue();

        _dbContext.ChangeTracker.Clear();
        (await _dbContext.Reports.SingleAsync(r => r.Id == report.Id)).Rate.Should().Be(5);
    }

    [Fact]
    public async Task GetOwnerIdAsync_ShouldReturnTheUserWhoSentTheReport_OrNullWhenMissing()
    {
        // Arrange
        var user = await SeedUserAsync();
        var location = await SeedLocationAsync();
        var report = await SeedReportAsync(user.Id, location.Id);

        LogArrange("arranged data", new { user, location, report });

        // Act
        var owner = await _repository.GetOwnerIdAsync(report.Id);
        var missing = await _repository.GetOwnerIdAsync(Guid.NewGuid());
        LogAct(new { owner, missing });

        // Assert
        owner.Should().Be(user.Id);
        missing.Should().BeNull();
    }

    #endregion

    #region FilterAsync

    [Fact]
    public async Task FilterAsync_ShouldOrderNewestFirstAndApplySkipAndTake()
    {
        // Arrange
        var user = await SeedUserAsync();
        var location = await SeedLocationAsync();
        var now = DateTime.UtcNow;
        var ids = new List<Guid>(); // newest first
        for (var i = 1; i <= 5; i++)
            ids.Add((await SeedReportAsync(user.Id, location.Id, reportDate: now.AddHours(-i))).Id);

        LogArrange("arranged data", new { user, location, ids });

        // Act
        var result = await _repository.FilterAsync(
            predicate: r => true,
            include: null,
            orderBy: r => r.ReportDate,
            orderByDesc: true,
            skip: 1,
            take: 2);
        LogAct(result);

        // Assert
        result.Select(r => r.Id).Should().Equal(ids[1], ids[2]);
    }

    [Fact]
    public async Task FilterAsync_ShouldLoadOnlyTheRequestedIncludes()
    {
        // Arrange
        var user = await SeedUserAsync();
        var location = await SeedLocationAsync("Library");
        var report = await SeedReportAsync(user.Id, location.Id);
        await SeedImageAsync(report.Id);

        LogArrange("arranged data", new { user, location, report });

        // Act
        var withIncludes = await _repository.FilterAsync(r => r.Id == report.Id, [r => r.Images, r => r.Location]);
        var withoutIncludes = await _repository.FilterAsync(r => r.Id == report.Id);
        LogAct(new { withIncludes, withoutIncludes });

        // Assert
        withIncludes.Single().Images.Should().HaveCount(1);
        withIncludes.Single().Location!.Title.Should().Be("Library");
        withoutIncludes.Single().Images.Should().BeEmpty();
        withoutIncludes.Single().Location.Should().BeNull();
    }

    #endregion

    #region LazyGetAllAsync (filters)

    [Fact]
    public async Task LazyGetAllAsync_ShouldReturnNewestFirstWithImages_WhenNoFilterIsSet()
    {
        // Arrange
        var user = await SeedUserAsync();
        var location = await SeedLocationAsync();
        var old = await SeedReportAsync(user.Id, location.Id, reportDate: DateTime.UtcNow.AddDays(-2));
        var recent = await SeedReportAsync(user.Id, location.Id, reportDate: DateTime.UtcNow.AddHours(-1));
        await SeedImageAsync(recent.Id);

        LogArrange("arranged data", new { user, location, old, recent });

        // Act
        var result = await _repository.LazyGetAllAsync(new ReportFilterDto(), Page());
        LogAct(result);

        // Assert
        result.Select(r => r.Id).Should().Equal(recent.Id, old.Id);
        result[0].Images.Should().HaveCount(1);
    }

    [Fact]
    public async Task LazyGetAllAsync_ShouldCombineFiltersWithAnd()
    {
        // Arrange
        var user = await SeedUserAsync();
        var locationA = await SeedLocationAsync();
        var locationB = await SeedLocationAsync();
        var match = await SeedReportAsync(user.Id, locationA.Id, ReportStateEnum.InProgress, category: ReportCategory.Plumbing);
        await SeedReportAsync(user.Id, locationA.Id, ReportStateEnum.Pending, category: ReportCategory.Plumbing);
        await SeedReportAsync(user.Id, locationA.Id, ReportStateEnum.InProgress, category: ReportCategory.Roads);
        await SeedReportAsync(user.Id, locationB.Id, ReportStateEnum.InProgress, category: ReportCategory.Plumbing);

        var filter = new ReportFilterDto
        {
            State = ReportStateEnum.InProgress,
            Category = ReportCategory.Plumbing,
            LocationId = locationA.Id
        };
        LogArrange(nameof(filter), filter);

        // Act
        var result = await _repository.LazyGetAllAsync(filter, Page());
        LogAct(result);

        // Assert
        result.Select(r => r.Id).Should().Equal(match.Id);
    }

    [Fact]
    public async Task LazyGetAllAsync_ShouldFilterBySeverityAndRate()
    {
        // Arrange
        var user = await SeedUserAsync();
        var location = await SeedLocationAsync();
        var match = await SeedReportAsync(user.Id, location.Id, ReportStateEnum.Fixed, rate: 4, severity: ReportSeverity.Large);
        await SeedReportAsync(user.Id, location.Id, ReportStateEnum.Fixed, rate: 5, severity: ReportSeverity.Large);
        await SeedReportAsync(user.Id, location.Id, ReportStateEnum.Fixed, rate: 4, severity: ReportSeverity.Small);
        await SeedReportAsync(user.Id, location.Id, ReportStateEnum.Fixed, severity: ReportSeverity.Large); // not rated

        LogArrange("arranged data", new { user, location, match });

        // Act
        var result = await _repository.LazyGetAllAsync(new ReportFilterDto { Severity = ReportSeverity.Large, Rate = 4 }, Page());
        LogAct(result);

        // Assert
        result.Select(r => r.Id).Should().Equal(match.Id);
    }

    [Fact]
    public async Task LazyGetAllAsync_ShouldFilterByDateRangeInclusive()
    {
        // Arrange
        var user = await SeedUserAsync();
        var location = await SeedLocationAsync();
        var from = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2025, 3, 31, 0, 0, 0, DateTimeKind.Utc);

        var before = await SeedReportAsync(user.Id, location.Id, reportDate: from.AddSeconds(-1));
        var onStart = await SeedReportAsync(user.Id, location.Id, reportDate: from);
        var inside = await SeedReportAsync(user.Id, location.Id, reportDate: from.AddDays(10));
        var onEnd = await SeedReportAsync(user.Id, location.Id, reportDate: to);
        var after = await SeedReportAsync(user.Id, location.Id, reportDate: to.AddSeconds(1));

        LogArrange("arranged data", new { user, location, from, to, before, onStart, inside, onEnd, after });

        // Act
        var result = await _repository.LazyGetAllAsync(new ReportFilterDto { FromDate = from, ToDate = to }, Page());
        LogAct(result);

        // Assert
        result.Select(r => r.Id).Should().BeEquivalentTo(new[] { onStart.Id, inside.Id, onEnd.Id });
        result.Select(r => r.Id).Should().NotContain(new[] { before.Id, after.Id });
    }

    [Fact]
    public async Task LazyGetAllAsync_ShouldFilterByDescriptionAndLocationTitleContainingTheText()
    {
        // Arrange
        var user = await SeedUserAsync();
        var library = await SeedLocationAsync("Central Library");
        var gym = await SeedLocationAsync("Sports Gym");
        var byDescription = await SeedReportAsync(user.Id, gym.Id, description: "Broken light in the hall");
        var byTitle = await SeedReportAsync(user.Id, library.Id, description: "Door is stuck");
        await SeedReportAsync(user.Id, gym.Id, description: "Door is stuck");

        LogArrange("arranged data", new { user, library, gym, byDescription, byTitle });

        // Act
        var descriptionResult = await _repository.LazyGetAllAsync(new ReportFilterDto { Description = "light" }, Page());
        var titleResult = await _repository.LazyGetAllAsync(new ReportFilterDto { Title = "Library" }, Page());
        LogAct(new { descriptionResult, titleResult });

        // Assert
        descriptionResult.Select(r => r.Id).Should().Equal(byDescription.Id);
        titleResult.Select(r => r.Id).Should().Equal(byTitle.Id);
    }

    [Fact]
    public async Task LazyGetAllAsync_ShouldApplySkipAndTake()
    {
        // Arrange
        var user = await SeedUserAsync();
        var location = await SeedLocationAsync();
        var ids = new List<Guid>(); // newest first
        for (var i = 1; i <= 5; i++)
            ids.Add((await SeedReportAsync(user.Id, location.Id, reportDate: DateTime.UtcNow.AddHours(-i))).Id);

        LogArrange("arranged data", new { user, location, ids });

        // Act
        var result = await _repository.LazyGetAllAsync(new ReportFilterDto(), Page(skip: 3, take: 5));
        LogAct(result);

        // Assert
        result.Select(r => r.Id).Should().Equal(ids[3], ids[4]);
    }

    #endregion

    #region UpdateState

    [Fact]
    public async Task UpdateState_ShouldReturnNull_WhenReportDoesNotExist()
    {
        var dto = new ReportUpdateStateDto { Id = Guid.NewGuid(), State = ReportStateEnum.Fixed };
        LogArrange(nameof(dto), dto);

        // Act
        var result = await _repository.UpdateState(dto);
        LogAct(result);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateState_ShouldSetTheStateAndFixDate_WhenReportBecomesFixed()
    {
        // Arrange
        var user = await SeedUserAsync();
        var location = await SeedLocationAsync();
        var report = await SeedReportAsync(user.Id, location.Id, ReportStateEnum.InProgress);
        var dto = new ReportUpdateStateDto { Id = report.Id, State = ReportStateEnum.Fixed };
        LogArrange(nameof(dto), dto);

        // Act
        var updated = await _repository.UpdateState(dto);
        var saved = await _repository.SaveChangesAsync();
        LogAct(updated);

        // Assert
        saved.Should().BeTrue();

        _dbContext.ChangeTracker.Clear();
        var stored = await _dbContext.Reports.SingleAsync(r => r.Id == report.Id);
        stored.State.Should().Be(ReportStateEnum.Fixed);
        stored.FixDate.Should().NotBeNull();
        stored.FixDate!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task UpdateState_ShouldClearRateAndFixDate_WhenAFixedReportIsReopened()
    {
        // Arrange
        var user = await SeedUserAsync();
        var location = await SeedLocationAsync();
        var report = await SeedReportAsync(user.Id, location.Id, ReportStateEnum.Fixed, rate: 5);
        var dto = new ReportUpdateStateDto { Id = report.Id, State = ReportStateEnum.NeedsFollowUp };
        LogArrange(nameof(dto), dto);

        // Act
        await _repository.UpdateState(dto);
        var saved = await _repository.SaveChangesAsync();
        LogAct(saved);

        // Assert
        saved.Should().BeTrue();

        _dbContext.ChangeTracker.Clear();
        var stored = await _dbContext.Reports.SingleAsync(r => r.Id == report.Id);
        stored.State.Should().Be(ReportStateEnum.NeedsFollowUp);
        stored.Rate.Should().BeNull();
        stored.FixDate.Should().BeNull();
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldReturnFalse_WhenTheReportAlreadyHadThatState()
    {
        // Arrange
        var user = await SeedUserAsync();
        var location = await SeedLocationAsync();
        var report = await SeedReportAsync(user.Id, location.Id, ReportStateEnum.Pending);

        LogArrange("arranged data", new { user, location, report });

        // Act
        await _repository.UpdateState(new ReportUpdateStateDto { Id = report.Id, State = ReportStateEnum.Pending });
        var saved = await _repository.SaveChangesAsync();
        LogAct(saved);

        // Assert: ReportService relies on this to skip the notification
        saved.Should().BeFalse();
    }

    #endregion

    #region RemoveById

    [Fact]
    public async Task RemoveById_ShouldReturnNull_WhenReportDoesNotExist()
    {
        var id = Guid.NewGuid();
        LogArrange(nameof(id), id);

        // Act
        var result = await _repository.RemoveById(id);
        LogAct(result);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveById_ShouldDeleteTheReportAndItsMessages_AndOrphanItsImages()
    {
        // Arrange
        var user = await SeedUserAsync();
        var location = await SeedLocationAsync();
        var report = await SeedReportAsync(user.Id, location.Id);
        var image = await SeedImageAsync(report.Id);
        var message = await SeedMessageAsync(report.Id, user.Id);
        var notification = await SeedNotificationAsync(user.Id, reportId: report.Id);

        LogArrange("arranged data", new { user, location, report, image, message, notification });

        // Act
        var removed = await _repository.RemoveById(report.Id);
        var saved = await _repository.SaveChangesAsync();
        LogAct(removed);

        // Assert
        removed.Should().NotBeNull();
        saved.Should().BeTrue();

        _dbContext.ChangeTracker.Clear();
        (await _dbContext.Reports.AnyAsync(r => r.Id == report.Id)).Should().BeFalse();
        (await _dbContext.Messages.AnyAsync(m => m.Id == message.Id)).Should().BeFalse();

        // the image stays as an orphan for the cleanup job, the notification stays without a report
        (await _dbContext.Images.SingleAsync(i => i.Id == image.Id)).ReportId.Should().BeNull();
        (await _dbContext.Notifications.SingleAsync(n => n.Id == notification.Id)).ReportId.Should().BeNull();
    }

    #endregion

    #region GetHistoryStatsAsync

    [Fact]
    public async Task GetHistoryStatsAsync_ShouldCountOnlyTheUsersReports()
    {
        // Arrange
        var user = await SeedUserAsync();
        var other = await SeedUserAsync();
        var location = await SeedLocationAsync();
        await SeedReportAsync(user.Id, location.Id, ReportStateEnum.Fixed, rate: 5);
        await SeedReportAsync(user.Id, location.Id, ReportStateEnum.Fixed); // fixed, not rated yet
        await SeedReportAsync(user.Id, location.Id, ReportStateEnum.InProgress);
        await SeedReportAsync(other.Id, location.Id, ReportStateEnum.Pending);

        LogArrange("arranged data", new { user, other, location });

        // Act
        var stats = await _repository.GetHistoryStatsAsync(user.Id);
        LogAct(stats);

        // Assert
        stats.TotalReportsCount.Should().Be(3);
        stats.InProgressReportsCount.Should().Be(1);
        stats.CompletedReportsCount.Should().Be(2);
        stats.AwaitingRatingReportsCount.Should().Be(1);
    }

    [Fact]
    public async Task GetHistoryStatsAsync_ShouldCountEveryonesReports_WhenUserIdIsNull()
    {
        // Arrange
        var user = await SeedUserAsync();
        var other = await SeedUserAsync();
        var location = await SeedLocationAsync();
        await SeedReportAsync(user.Id, location.Id, ReportStateEnum.Fixed);
        await SeedReportAsync(other.Id, location.Id, ReportStateEnum.InProgress);
        await SeedReportAsync(other.Id, location.Id, ReportStateEnum.Pending);

        LogArrange("arranged data", new { user, other, location });

        // Act
        var stats = await _repository.GetHistoryStatsAsync(null);
        LogAct(stats);

        // Assert
        stats.TotalReportsCount.Should().Be(3);
        stats.InProgressReportsCount.Should().Be(1);
        stats.CompletedReportsCount.Should().Be(1);
        stats.AwaitingRatingReportsCount.Should().Be(1);
    }

    [Fact]
    public async Task GetHistoryStatsAsync_ShouldReturnZeros_WhenTheUserHasNoReports()
    {
        // Arrange
        var user = await SeedUserAsync();

        LogArrange(nameof(user), user);

        // Act
        var stats = await _repository.GetHistoryStatsAsync(user.Id);
        LogAct(stats);

        // Assert
        stats.TotalReportsCount.Should().Be(0);
        stats.InProgressReportsCount.Should().Be(0);
        stats.CompletedReportsCount.Should().Be(0);
        stats.AwaitingRatingReportsCount.Should().Be(0);
    }

    #endregion

    // GetMaintenanceStatsAsync is not tested here: it uses EF.Functions.DateDiffMinute, which only exists for SQL Server,
    // so it cannot run on SQLite. Test it against a real SQL Server (e.g. Testcontainers) if you need it covered.
}