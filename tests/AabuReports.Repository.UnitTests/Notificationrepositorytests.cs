using AabuReports.Core.Dtos.LazyLoading;
using AabuReports.Core.Entities;
using AabuReports.Core.Enums;
using AabuReports.Infrastructure.Repositories;
using AutoFixture;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace AabuReports.Repository.UnitTests;

public class NotificationRepositoryTests : RepositoryTestBase
{
    private readonly NotificationRepository _repository;

    public NotificationRepositoryTests(ITestOutputHelper output) : base(output)
    {
        _repository = new NotificationRepository(_dbContext);
    }

    [Fact]
    public async Task AddNotification_ShouldPersistTheNotificationAsUnread_WhenChangesAreSaved()
    {
        // Arrange
        var user = await SeedUserAsync();
        var notification = new Notification
        {
            Type = NotificationType.ReportFixed,
            Message = _fixture.Create<string>(),
            Subject = "Library",
            CreatedAt = DateTime.UtcNow,
            UserId = user.Id
        };
        LogArrange(nameof(notification), notification);

        // Act
        _repository.AddNotification(notification);
        var saved = await _repository.SaveChangesAsync();
        LogAct(saved);

        // Assert
        saved.Should().BeTrue();

        _dbContext.ChangeTracker.Clear();
        var stored = await _dbContext.Notifications.SingleAsync(n => n.Id == notification.Id);
        stored.Type.Should().Be(NotificationType.ReportFixed);
        stored.Subject.Should().Be("Library");
        stored.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task LazyGetByUserIdAsync_ShouldReturnOnlyTheUsersNotificationsNewestFirst()
    {
        // Arrange
        var user = await SeedUserAsync();
        var other = await SeedUserAsync();
        var now = DateTime.UtcNow;

        var oldest = await SeedNotificationAsync(user.Id, now.AddMinutes(-30));
        var newest = await SeedNotificationAsync(user.Id, now.AddMinutes(-1));
        var middle = await SeedNotificationAsync(user.Id, now.AddMinutes(-10));
        await SeedNotificationAsync(other.Id, now);

        LogArrange("arranged data", new { user, other, oldest, newest, middle });

        // Act
        var result = await _repository.LazyGetByUserIdAsync(user.Id, new LazyLoadingDto { Skip = 0, Take = 10 });
        LogAct(result);

        // Assert
        result.Select(n => n.Id).Should().Equal(newest.Id, middle.Id, oldest.Id);
        _dbContext.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task LazyGetByUserIdAsync_ShouldApplySkipAndTake()
    {
        // Arrange
        var user = await SeedUserAsync();
        var now = DateTime.UtcNow;
        var ids = new List<Guid>(); // newest first
        for (var i = 1; i <= 5; i++)
            ids.Add((await SeedNotificationAsync(user.Id, now.AddMinutes(-i))).Id);

        LogArrange("arranged data", new { user, ids });

        // Act
        var result = await _repository.LazyGetByUserIdAsync(user.Id, new LazyLoadingDto { Skip = 1, Take = 2 });
        LogAct(result);

        // Assert
        result.Select(n => n.Id).Should().Equal(ids[1], ids[2]);
    }

    [Fact]
    public async Task LazyGetByUserIdAsync_ShouldReturnOnlyUnread_WhenUnreadOnlyIsTrue()
    {
        // Arrange
        var user = await SeedUserAsync();
        var unread = await SeedNotificationAsync(user.Id, isRead: false);
        await SeedNotificationAsync(user.Id, isRead: true);

        LogArrange("arranged data", new { user, unread });

        // Act
        var result = await _repository.LazyGetByUserIdAsync(user.Id, new LazyLoadingDto { Skip = 0, Take = 10 }, unreadOnly: true);
        LogAct(result);

        // Assert
        result.Select(n => n.Id).Should().Equal(unread.Id);
    }

    [Fact]
    public async Task CountUnreadAsync_ShouldCountOnlyTheUsersUnreadNotifications()
    {
        // Arrange
        var user = await SeedUserAsync();
        var other = await SeedUserAsync();
        await SeedNotificationAsync(user.Id);
        await SeedNotificationAsync(user.Id);
        await SeedNotificationAsync(user.Id, isRead: true);
        await SeedNotificationAsync(other.Id);

        LogArrange("arranged data", new { user, other });

        // Act
        var count = await _repository.CountUnreadAsync(user.Id);
        LogAct(count);

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_ShouldMarkOnlyTheUsersUnreadNotifications()
    {
        // Arrange
        var user = await SeedUserAsync();
        var other = await SeedUserAsync();
        await SeedNotificationAsync(user.Id);
        await SeedNotificationAsync(user.Id);
        await SeedNotificationAsync(user.Id);
        await SeedNotificationAsync(user.Id, isRead: true);
        await SeedNotificationAsync(other.Id);
        await SeedNotificationAsync(other.Id);

        LogArrange("arranged data", new { user, other });

        // Act
        var changed = await _repository.MarkAllAsReadAsync(user.Id);
        LogAct(changed);

        // Assert: the already read one is not counted
        changed.Should().Be(3);

        _dbContext.ChangeTracker.Clear();
        (await _dbContext.Notifications.CountAsync(n => n.UserId == user.Id && !n.IsRead)).Should().Be(0);
        (await _dbContext.Notifications.CountAsync(n => n.UserId == other.Id && !n.IsRead)).Should().Be(2);
    }

    [Fact]
    public async Task GetTrackedByIdAsync_ShouldReturnATrackedNotification_WhenItBelongsToTheUser()
    {
        // Arrange
        var user = await SeedUserAsync();
        var notification = await SeedNotificationAsync(user.Id);
        LogArrange(nameof(notification), notification);

        // Act
        var result = await _repository.GetTrackedByIdAsync(notification.Id, user.Id);
        LogAct(result);

        // Assert
        result.Should().NotBeNull();
        _dbContext.ChangeTracker.Entries<Notification>().Should().HaveCount(1);

        // changes on it are saved by SaveChangesAsync
        result!.MarkAsRead().Should().BeTrue();
        (await _repository.SaveChangesAsync()).Should().BeTrue();

        _dbContext.ChangeTracker.Clear();
        (await _dbContext.Notifications.SingleAsync(n => n.Id == notification.Id)).IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task GetTrackedByIdAsync_ShouldReturnNull_WhenItBelongsToAnotherUser()
    {
        // Arrange
        var owner = await SeedUserAsync();
        var other = await SeedUserAsync();
        var notification = await SeedNotificationAsync(owner.Id);

        LogArrange("arranged data", new { owner, other, notification });

        // Act
        var result = await _repository.GetTrackedByIdAsync(notification.Id, other.Id);
        LogAct(result);

        // Assert
        result.Should().BeNull();
    }
}