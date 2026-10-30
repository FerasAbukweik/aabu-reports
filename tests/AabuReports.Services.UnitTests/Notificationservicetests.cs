using System.Net;
using AabuReports.Core.Abstractions.Repositories;
using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.LazyLoading;
using AabuReports.Core.Dtos.Notifications;
using AabuReports.Core.Entities;
using AabuReports.Core.Enums;
using AabuReports.Infrastructure.Services;
using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit.Abstractions;

namespace AabuReports.Services.UnitTests;

public class NotificationServiceTests : ServiceTestBase
{
    private readonly INotificationService _notificationService;
    private readonly Mock<INotificationRepository> _repositoryMock;
    private readonly Mock<INotificationPusher> _pusherMock;

    public NotificationServiceTests(ITestOutputHelper output) : base(output)
    {
        _repositoryMock = new Mock<INotificationRepository>();
        _pusherMock = new Mock<INotificationPusher>();

        _notificationService = new NotificationService(
            _repositoryMock.Object,
            _pusherMock.Object,
            NullLogger<NotificationService>.Instance);
    }

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_ShouldReturnInternalServerErrorAndNotPush_WhenNothingWasSaved()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var message = Fixture.Create<string>();
        LogArrange(nameof(userId), userId);
        LogArrange(nameof(message), message);

        _repositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var result = await _notificationService.CreateAsync(userId, NotificationType.System, message, null, null, CancellationToken.None);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        result.ErrorMessage.Should().Be("no changes where applied to the DB");

        _pusherMock.Verify(
            p => p.PushAsync(It.IsAny<Guid>(), It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ShouldSaveAndPushNotification_WhenSaveSucceeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var message = Fixture.Create<string>();
        var subject = Fixture.Create<string>();
        LogArrange(nameof(userId), userId);
        LogArrange(nameof(message), message);

        Notification? added = null;
        _repositoryMock.Setup(r => r.AddNotification(It.IsAny<Notification>())).Callback<Notification>(n => added = n);
        _repositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await _notificationService.CreateAsync(userId, NotificationType.ReportFixed, message, subject, reportId, CancellationToken.None);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.Created);

        added.Should().NotBeNull();
        added!.UserId.Should().Be(userId);
        added.ReportId.Should().Be(reportId);

        result.Value!.Id.Should().Be(added.Id);
        result.Value.Type.Should().Be(NotificationType.ReportFixed);
        result.Value.Message.Should().Be(message);
        result.Value.Subject.Should().Be(subject);
        result.Value.IsRead.Should().BeFalse();

        _pusherMock.Verify(p => p.PushAsync(userId, result.Value, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ShouldStillSucceed_WhenPushingFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var message = Fixture.Create<string>();
        LogArrange(nameof(userId), userId);

        _repositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _pusherMock.Setup(p => p.PushAsync(It.IsAny<Guid>(), It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("signalr is down"));

        // Act
        var result = await _notificationService.CreateAsync(userId, NotificationType.System, message, null, null, CancellationToken.None);
        LogAct(result);

        // Assert: the notification is already saved, so the user can still see it in the list
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateAsync_ShouldPropagate_WhenPushIsCancelled()
    {
        // Arrange
        _repositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _pusherMock.Setup(p => p.PushAsync(It.IsAny<Guid>(), It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var act = () => _notificationService.CreateAsync(Guid.NewGuid(), NotificationType.System, "x", null, null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region LazyGetByUserIdAsync

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LazyGetByUserIdAsync_ShouldReturnMappedNotifications(bool unreadOnly)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var lazy = new LazyLoadingDto { Skip = 0, Take = 10 };
        IReadOnlyList<Notification> notifications = new List<Notification>
        {
            CreateNotification(userId),
            CreateNotification(userId)
        };
        LogArrange(nameof(notifications), notifications);

        _repositoryMock
            .Setup(r => r.LazyGetByUserIdAsync(userId, lazy, unreadOnly, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notifications);

        // Act
        var result = await _notificationService.LazyGetByUserIdAsync(userId, lazy, unreadOnly);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value!.Select(n => n.Id).Should().Equal(notifications.Select(n => n.Id));
    }

    #endregion

    #region GetUnreadCountAsync

    [Fact]
    public async Task GetUnreadCountAsync_ShouldReturnTheCountFromTheRepository()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _repositoryMock.Setup(r => r.CountUnreadAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(7);

        // Act
        var result = await _notificationService.GetUnreadCountAsync(userId);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(7);
    }

    #endregion

    #region MarkAllAsReadAsync

    [Fact]
    public async Task MarkAllAsReadAsync_ShouldMarkNotificationsAndReturnNoContent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _repositoryMock.Setup(r => r.MarkAllAsReadAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(3);

        // Act
        var result = await _notificationService.MarkAllAsReadAsync(userId);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.NoContent);
        _repositoryMock.Verify(r => r.MarkAllAsReadAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}