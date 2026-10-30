using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace AabuReports.Repository.UnitTests;

/// <summary> the rules configured in OnModelCreating (the services and the cleanup job depend on them) </summary>
public class ApplicationDbContextTests : RepositoryTestBase
{
    public ApplicationDbContextTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task DeletingAReport_ShouldDeleteItsMessages_AndSetImageAndNotificationReportIdToNull()
    {
        // Arrange
        var user = await SeedUserAsync();
        var location = await SeedLocationAsync();
        var report = await SeedReportAsync(user.Id, location.Id);
        var image = await SeedImageAsync(report.Id);
        var message = await SeedMessageAsync(report.Id, user.Id);
        var notification = await SeedNotificationAsync(user.Id, reportId: report.Id);
        LogArrange(nameof(report), report);

        // Act
        _dbContext.Reports.Remove((await _dbContext.Reports.FindAsync(report.Id))!);
        var saved = await _dbContext.SaveChangesAsync();
        LogAct(saved);

        // Assert
        _dbContext.ChangeTracker.Clear();
        (await _dbContext.Messages.AnyAsync(m => m.Id == message.Id)).Should().BeFalse();
        (await _dbContext.Images.SingleAsync(i => i.Id == image.Id)).ReportId.Should().BeNull();
        (await _dbContext.Notifications.SingleAsync(n => n.Id == notification.Id)).ReportId.Should().BeNull();
    }

    [Fact]
    public async Task DeletingALocation_ShouldDeleteItsReports()
    {
        // Arrange
        var user = await SeedUserAsync();
        var location = await SeedLocationAsync();
        var report = await SeedReportAsync(user.Id, location.Id);
        LogArrange(nameof(location), location);

        // Act
        _dbContext.Locations.Remove((await _dbContext.Locations.FindAsync(location.Id))!);
        var saved = await _dbContext.SaveChangesAsync();
        LogAct(saved);

        // Assert
        _dbContext.ChangeTracker.Clear();
        (await _dbContext.Reports.AnyAsync(r => r.Id == report.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task DeletingAUser_ShouldDeleteTheirRefreshTokensAndNotifications()
    {
        // Arrange
        var user = await SeedUserAsync();
        var token = await SeedRefreshTokenAsync(user.Id);
        var notification = await SeedNotificationAsync(user.Id);
        LogArrange(nameof(user), user);

        // Act
        _dbContext.Users.Remove((await _dbContext.Users.FindAsync(user.Id))!);
        var saved = await _dbContext.SaveChangesAsync();
        LogAct(saved);

        // Assert
        _dbContext.ChangeTracker.Clear();
        (await _dbContext.RefreshTokens.AnyAsync(t => t.Id == token.Id)).Should().BeFalse();
        (await _dbContext.Notifications.AnyAsync(n => n.Id == notification.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task DeletingAUserWithReports_ShouldBeRejected()
    {
        // Arrange
        var user = await SeedUserAsync();
        var location = await SeedLocationAsync();
        await SeedReportAsync(user.Id, location.Id);
        LogArrange(nameof(user), user);

        _dbContext.Users.Remove((await _dbContext.Users.FindAsync(user.Id))!);

        // Act
        var act = () => _dbContext.SaveChangesAsync();
        LogAct("deleting a user that has reports");

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task TwoUsers_ShouldNotShareTheSameStudentId()
    {
        // Arrange
        var first = await SeedUserAsync();
        var second = CreateUser();
        second.StudentId = first.StudentId; // different UserName / Email, same student id
        LogArrange(nameof(second), second);

        _dbContext.Users.Add(second);

        // Act
        var act = () => _dbContext.SaveChangesAsync();
        LogAct("adding a user with an existing student id");

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task DateTimes_ShouldBeReadBackAsUtc()
    {
        // Arrange
        var user = await SeedUserAsync();
        var location = await SeedLocationAsync();
        var reportDate = new DateTime(2025, 5, 17, 10, 30, 0, DateTimeKind.Utc);
        var report = await SeedReportAsync(user.Id, location.Id, reportDate: reportDate);
        LogArrange(nameof(reportDate), reportDate);

        // Act
        var stored = await _dbContext.Reports.AsNoTracking().SingleAsync(r => r.Id == report.Id);
        LogAct(stored.ReportDate);

        // Assert
        stored.ReportDate.Kind.Should().Be(DateTimeKind.Utc);
        stored.ReportDate.Should().Be(reportDate);
    }
}