using System.Net;
using AabuReports.Core.Abstractions.Repositories;
using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.LazyLoading;
using AabuReports.Core.Dtos.Notifications;
using AabuReports.Core.Entities;
using AabuReports.Core.Entities.Identity;
using AabuReports.Core.Enums;
using AabuReports.Core.Results;
using AabuReports.Infrastructure.Services;
using AutoFixture;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit.Abstractions;

namespace AabuReports.Services.UnitTests;

public class MessageServiceTests : ServiceTestBase
{
    private readonly IMessageService _messageService;
    private readonly Mock<IMessageRepository> _messageRepositoryMock;
    private readonly Mock<IReportRepository> _reportRepositoryMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;

    public MessageServiceTests(ITestOutputHelper output) : base(output)
    {
        _messageRepositoryMock = new Mock<IMessageRepository>();
        _reportRepositoryMock = new Mock<IReportRepository>();
        _notificationServiceMock = new Mock<INotificationService>();
        _userManagerMock = CreateUserManagerMock();

        _messageService = new MessageService(
            _messageRepositoryMock.Object,
            _reportRepositoryMock.Object,
            _notificationServiceMock.Object,
            _userManagerMock.Object,
            NullLogger<MessageService>.Instance);
    }

    private void SetupNotificationResult(Result<NotificationDto> result)
    {
        _notificationServiceMock
            .Setup(n => n.CreateAsync(
                It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }

    #region AddAsync

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddAsync_ShouldReturnBadRequest_WhenContentIsEmpty(string content)
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        LogArrange(nameof(content), content);

        // Act
        var result = await _messageService.AddAsync(reportId, senderId, content);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.ErrorMessage.Should().Be("The message cannot be empty");

        _userManagerMock.Verify(m => m.FindByIdAsync(It.IsAny<string>()), Times.Never);
        _messageRepositoryMock.Verify(r => r.AddMessage(It.IsAny<Message>()), Times.Never);
    }

    [Fact]
    public async Task AddAsync_ShouldRejectOversizedMessagesBeforeQueryingOrSaving()
    {
        var result = await _messageService.AddAsync(Guid.NewGuid(), Guid.NewGuid(), new string('x', 501));

        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _userManagerMock.Verify(m => m.FindByIdAsync(It.IsAny<string>()), Times.Never);
        _messageRepositoryMock.Verify(r => r.AddMessage(It.IsAny<Message>()), Times.Never);
    }

    [Fact]
    public async Task AddAsync_ShouldReturnNotFound_WhenSenderDoesNotExist()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        LogArrange(nameof(senderId), senderId);

        _userManagerMock.Setup(m => m.FindByIdAsync(senderId.ToString())).ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _messageService.AddAsync(reportId, senderId, "hello");
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
        result.ErrorMessage.Should().Be("User not found");
    }

    [Fact]
    public async Task AddAsync_ShouldReturnInternalServerError_WhenNothingWasSaved()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var sender = CreateUser();
        LogArrange(nameof(sender), sender);

        _userManagerMock.Setup(m => m.FindByIdAsync(sender.Id.ToString())).ReturnsAsync(sender);
        _messageRepositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        _reportRepositoryMock.Setup(r => r.GetOwnerIdAsync(reportId, It.IsAny<CancellationToken>())).ReturnsAsync(sender.Id);

        // Act
        var result = await _messageService.AddAsync(reportId, sender.Id, "hello");
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        result.ErrorMessage.Should().Be("no changes where applied to the DB");

        _reportRepositoryMock.Verify(r => r.GetOwnerIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddAsync_ShouldReturnNotFound_WhenReportDoesNotExist()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var sender = CreateUser();
        LogArrange(nameof(reportId), reportId);

        _userManagerMock.Setup(m => m.FindByIdAsync(sender.Id.ToString())).ReturnsAsync(sender);
        _messageRepositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _reportRepositoryMock.Setup(r => r.GetOwnerIdAsync(reportId, It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var result = await _messageService.AddAsync(reportId, sender.Id, "hello");
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
        result.ErrorMessage.Should().Be("Report not found");
        _messageRepositoryMock.Verify(r => r.AddMessage(It.IsAny<Message>()), Times.Never);
        _messageRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddAsync_ShouldNotNotify_WhenSenderIsTheOwnerOfTheReport()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var sender = CreateUser();
        LogArrange(nameof(sender), sender);

        _userManagerMock.Setup(m => m.FindByIdAsync(sender.Id.ToString())).ReturnsAsync(sender);
        _messageRepositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _reportRepositoryMock.Setup(r => r.GetOwnerIdAsync(reportId, It.IsAny<CancellationToken>())).ReturnsAsync(sender.Id);

        // Act
        var result = await _messageService.AddAsync(reportId, sender.Id, "hello");
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.Created);

        _notificationServiceMock.Verify(
            n => n.CreateAsync(
                It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AddAsync_ShouldSaveTrimmedMessageAndNotifyOwner_WhenSenderIsNotTheOwner()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var sender = CreateUser();
        var report = CreateReport(userId: ownerId);
        LogArrange(nameof(sender), sender);
        LogArrange(nameof(report), report);

        Message? added = null;
        _userManagerMock.Setup(m => m.FindByIdAsync(sender.Id.ToString())).ReturnsAsync(sender);
        _messageRepositoryMock.Setup(r => r.AddMessage(It.IsAny<Message>())).Callback<Message>(m => added = m);
        _messageRepositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _reportRepositoryMock.Setup(r => r.GetOwnerIdAsync(reportId, It.IsAny<CancellationToken>())).ReturnsAsync(ownerId);
        _reportRepositoryMock.Setup(r => r.GetByIdAsync(reportId, It.IsAny<CancellationToken>())).ReturnsAsync(report);
        SetupNotificationResult(Result<NotificationDto>.Success(Fixture.Create<NotificationDto>(), HttpStatusCode.Created));

        // Act
        var result = await _messageService.AddAsync(reportId, sender.Id, "  hello owner  ");
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Value!.Content.Should().Be("hello owner");
        result.Value.SenderId.Should().Be(sender.Id);
        result.Value.SenderName.Should().Be(sender.FullName);
        result.Value.ReportId.Should().Be(reportId);

        added.Should().NotBeNull();
        added!.Content.Should().Be("hello owner");

        // the location title is the subject of the notification
        _notificationServiceMock.Verify(
            n => n.CreateAsync(ownerId, NotificationType.NewMessage, It.IsAny<string>(), report.Location!.Title, reportId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AddAsync_ShouldStillSucceed_WhenCreatingTheNotificationFails(bool throwsException)
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var sender = CreateUser();
        LogArrange(nameof(sender), sender);

        _userManagerMock.Setup(m => m.FindByIdAsync(sender.Id.ToString())).ReturnsAsync(sender);
        _messageRepositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _reportRepositoryMock.Setup(r => r.GetOwnerIdAsync(reportId, It.IsAny<CancellationToken>())).ReturnsAsync(ownerId);
        _reportRepositoryMock.Setup(r => r.GetByIdAsync(reportId, It.IsAny<CancellationToken>())).ReturnsAsync((Report?)null);
        if (throwsException)
            _notificationServiceMock.Setup(n => n.CreateAsync(It.IsAny<Guid>(), It.IsAny<NotificationType>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("notification unavailable"));
        else
            SetupNotificationResult(Result<NotificationDto>.Failure("boom", HttpStatusCode.InternalServerError));

        // Act
        var result = await _messageService.AddAsync(reportId, sender.Id, "hello");
        LogAct(result);

        // Assert: the message is already saved, a failed notification must not fail it
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.Created);

        // the report could not be loaded, so the notification has no subject
        _notificationServiceMock.Verify(
            n => n.CreateAsync(ownerId, NotificationType.NewMessage, It.IsAny<string>(), (string?)null, reportId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region LazyGetByReportIdAsync

    [Fact]
    public async Task LazyGetByReportIdAsync_ShouldReturnMappedMessages()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var lazy = new LazyLoadingDto { Skip = 5, Take = 20 };
        var sender = CreateUser();
        IReadOnlyList<Message> messages = new List<Message>
        {
            new() { Content = "first", SentAt = DateTime.UtcNow, SenderId = sender.Id, Sender = sender, ReportId = reportId },
            new() { Content = "second", SentAt = DateTime.UtcNow, SenderId = sender.Id, Sender = sender, ReportId = reportId }
        };
        LogArrange(nameof(messages), messages);

        _messageRepositoryMock.Setup(r => r.LazyGetByReportIdAsync(reportId, lazy, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages);

        // Act
        var result = await _messageService.LazyGetByReportIdAsync(reportId, lazy);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value!.Select(m => m.Content).Should().Equal("first", "second");
        result.Value.Should().OnlyContain(m => m.SenderName == sender.FullName && m.ReportId == reportId);
    }

    #endregion
}
