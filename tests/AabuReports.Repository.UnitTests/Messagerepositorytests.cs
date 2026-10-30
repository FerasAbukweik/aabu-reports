using AabuReports.Core.Dtos.LazyLoading;
using AabuReports.Core.Entities;
using AabuReports.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace AabuReports.Repository.UnitTests;

public class MessageRepositoryTests : RepositoryTestBase
{
    private readonly MessageRepository _repository;

    public MessageRepositoryTests(ITestOutputHelper output) : base(output)
    {
        _repository = new MessageRepository(_dbContext);
    }

    [Fact]
    public async Task AddMessage_ShouldPersistTheMessage_WhenChangesAreSaved()
    {
        // Arrange
        var user = await SeedUserAsync();
        var location = await SeedLocationAsync();
        var report = await SeedReportAsync(user.Id, location.Id);
        var message = new Message
        {
            Content = "hello",
            SentAt = DateTime.UtcNow,
            SenderId = user.Id,
            ReportId = report.Id
        };
        LogArrange(nameof(message), message);

        // Act
        _repository.AddMessage(message);
        var saved = await _repository.SaveChangesAsync();
        LogAct(saved);

        // Assert
        saved.Should().BeTrue();

        _dbContext.ChangeTracker.Clear();
        var stored = await _dbContext.Messages.SingleAsync(m => m.Id == message.Id);
        stored.Content.Should().Be("hello");
        stored.ReportId.Should().Be(report.Id);
        stored.SenderId.Should().Be(user.Id);
    }

    [Fact]
    public async Task LazyGetByReportIdAsync_ShouldReturnOnlyThatReportsMessagesNewestFirstWithTheirSender()
    {
        // Arrange
        var user = await SeedUserAsync();
        var location = await SeedLocationAsync();
        var report = await SeedReportAsync(user.Id, location.Id);
        var otherReport = await SeedReportAsync(user.Id, location.Id);
        var now = DateTime.UtcNow;

        var oldest = await SeedMessageAsync(report.Id, user.Id, now.AddMinutes(-30));
        var newest = await SeedMessageAsync(report.Id, user.Id, now.AddMinutes(-1));
        var middle = await SeedMessageAsync(report.Id, user.Id, now.AddMinutes(-10));
        await SeedMessageAsync(otherReport.Id, user.Id, now);

        LogArrange("arranged data", new { user, location, report, otherReport, oldest, newest, middle });

        // Act
        var result = await _repository.LazyGetByReportIdAsync(report.Id, new LazyLoadingDto { Skip = 0, Take = 10 });
        LogAct(result);

        // Assert
        result.Select(m => m.Id).Should().Equal(newest.Id, middle.Id, oldest.Id);
        result.Should().OnlyContain(m => m.Sender != null && m.Sender.Id == user.Id);
        result[0].Sender!.FullName.Should().Be(user.FullName);
        _dbContext.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task LazyGetByReportIdAsync_ShouldApplySkipAndTake()
    {
        // Arrange
        var user = await SeedUserAsync();
        var location = await SeedLocationAsync();
        var report = await SeedReportAsync(user.Id, location.Id);
        var now = DateTime.UtcNow;
        var ids = new List<Guid>(); // newest first
        for (var i = 1; i <= 5; i++)
            ids.Add((await SeedMessageAsync(report.Id, user.Id, now.AddMinutes(-i))).Id);

        LogArrange("arranged data", new { user, location, report, ids });

        // Act
        var result = await _repository.LazyGetByReportIdAsync(report.Id, new LazyLoadingDto { Skip = 2, Take = 2 });
        LogAct(result);

        // Assert
        result.Select(m => m.Id).Should().Equal(ids[2], ids[3]);
    }

    [Fact]
    public async Task LazyGetByReportIdAsync_ShouldReturnEmpty_WhenTheReportHasNoMessages()
    {
        // Arrange
        var id = Guid.NewGuid();
        LogArrange(nameof(id), id);

        // Act
        var result = await _repository.LazyGetByReportIdAsync(id, new LazyLoadingDto { Skip = 0, Take = 10 });
        LogAct(result);

        // Assert
        result.Should().BeEmpty();
    }
}