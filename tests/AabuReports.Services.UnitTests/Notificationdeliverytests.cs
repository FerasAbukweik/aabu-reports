using AabuReports.Core.Dtos.Notifications;
using AabuReports.Core.Enums;
using AabuReports.Infrastructure;
using AabuReports.Webapi.Hubs.AppHub;
using AabuReports.Webapi.Services;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AabuReports.Services.UnitTests;

public class NotificationDeliveryTests
{
    [Fact]
    public async Task PushAsync_ShouldWaitForCommitBeforePublishing_AndPublishOnlyOnce()
    {
        // Arrange
        var (pusher, database, client) = CreatePusher();
        database.SetupGet(d => d.CurrentTransaction).Returns(Mock.Of<IDbContextTransaction>());
        var notification = CreateNotification();

        // Act
        await pusher.PushAsync(Guid.NewGuid(), notification);

        // Assert: nothing is visible while the transaction can still roll back.
        client.Verify(c => c.ReceiveNotification(It.IsAny<NotificationDto>()), Times.Never);

        // The transaction filter publishes the queue only after commit succeeds.
        database.SetupGet(d => d.CurrentTransaction).Returns((IDbContextTransaction?)null);
        await pusher.PublishPendingAsync();
        await pusher.PublishPendingAsync();
        client.Verify(c => c.ReceiveNotification(notification), Times.Once);
    }

    [Fact]
    public async Task DiscardPending_ShouldNeverPublishRolledBackNotifications()
    {
        // Arrange
        var (pusher, database, client) = CreatePusher();
        database.SetupGet(d => d.CurrentTransaction).Returns(Mock.Of<IDbContextTransaction>());
        await pusher.PushAsync(Guid.NewGuid(), CreateNotification());

        // Act
        pusher.DiscardPending();
        await pusher.PublishPendingAsync();

        // Assert
        client.Verify(c => c.ReceiveNotification(It.IsAny<NotificationDto>()), Times.Never);
    }

    [Fact]
    public async Task PublishPendingAsync_ShouldNotFailCommittedOperation_WhenLiveDeliveryFails()
    {
        // Arrange
        var (pusher, database, client) = CreatePusher();
        database.SetupGet(d => d.CurrentTransaction).Returns(Mock.Of<IDbContextTransaction>());
        client.SetupSequence(c => c.ReceiveNotification(It.IsAny<NotificationDto>()))
            .ThrowsAsync(new InvalidOperationException("Connection lost"))
            .Returns(Task.CompletedTask);
        await pusher.PushAsync(Guid.NewGuid(), CreateNotification());
        await pusher.PushAsync(Guid.NewGuid(), CreateNotification());

        // Act
        var publish = () => pusher.PublishPendingAsync();

        // Assert
        await publish.Should().NotThrowAsync();
        client.Verify(c => c.ReceiveNotification(It.IsAny<NotificationDto>()), Times.Exactly(2));
    }

    private static (SignalRNotificationPusher, Mock<DatabaseFacade>, Mock<IAppClient>) CreatePusher()
    {
        var context = new Mock<ApplicationDbContext>(new DbContextOptions<ApplicationDbContext>());
        var database = new Mock<DatabaseFacade>(context.Object);
        context.SetupGet(c => c.Database).Returns(database.Object);
        var client = new Mock<IAppClient>();
        client.Setup(c => c.ReceiveNotification(It.IsAny<NotificationDto>())).Returns(Task.CompletedTask);
        var clients = new Mock<IHubClients<IAppClient>>();
        clients.Setup(c => c.User(It.IsAny<string>())).Returns(client.Object);
        var hub = new Mock<IHubContext<AppHub, IAppClient>>();
        hub.SetupGet(h => h.Clients).Returns(clients.Object);

        return (new SignalRNotificationPusher(hub.Object, context.Object,
            NullLogger<SignalRNotificationPusher>.Instance), database, client);
    }

    private static NotificationDto CreateNotification() => new()
    {
        Id = Guid.NewGuid(), Type = NotificationType.ReportReceived,
        Message = "Report received", CreatedAt = DateTime.UtcNow, IsRead = false
    };
}
