using System.Text;
using System.Text.Json;
using AabuReports.Core.Dtos.Reports;
using AabuReports.Infrastructure.Services;
using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Moq;
using StackExchange.Redis;
using Xunit.Abstractions;

namespace AabuReports.Services.UnitTests;

public class RedisServiceTests : ServiceTestBase
{
    private readonly Mock<IDistributedCache> _cacheMock;

    public RedisServiceTests(ITestOutputHelper output) : base(output)
    {
        _cacheMock = new Mock<IDistributedCache>();
    }

    private RedisService CreateService(Dictionary<string, string?>? settings = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings ?? new Dictionary<string, string?>())
            .Build();

        return new RedisService(_cacheMock.Object, Mock.Of<IConnectionMultiplexer>(), configuration);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnDefault_WhenKeyIsNotCached()
    {
        // Arrange
        var key = Fixture.Create<string>();
        LogArrange(nameof(key), key);

        _cacheMock.Setup(c => c.GetAsync(key, It.IsAny<CancellationToken>())).ReturnsAsync((byte[]?)null);

        // Act
        var result = await CreateService().GetAsync<MaintenanceStatsDto>(key);
        LogAct(result);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ShouldDeserializeTheCachedJson_WhenKeyIsCached()
    {
        // Arrange
        var key = Fixture.Create<string>();
        var stats = Fixture.Create<MaintenanceStatsDto>();
        LogArrange(nameof(stats), stats);

        var bytes = JsonSerializer.SerializeToUtf8Bytes(stats);
        _cacheMock.Setup(c => c.GetAsync(key, It.IsAny<CancellationToken>())).ReturnsAsync(bytes);

        // Act
        var result = await CreateService().GetAsync<MaintenanceStatsDto>(key);
        LogAct(result);

        // Assert
        result.Should().BeEquivalentTo(stats);
    }

    [Fact]
    public async Task SetAsync_ShouldStoreJsonWithTheDefaultExpirations_WhenNothingIsConfigured()
    {
        // Arrange
        var key = Fixture.Create<string>();
        var stats = Fixture.Create<MaintenanceStatsDto>();
        LogArrange(nameof(stats), stats);

        byte[]? savedBytes = null;
        DistributedCacheEntryOptions? savedOptions = null;
        _cacheMock
            .Setup(c => c.SetAsync(key, It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>((_, bytes, options, _) =>
            {
                savedBytes = bytes;
                savedOptions = options;
            })
            .Returns(Task.CompletedTask);

        // Act
        await CreateService().SetAsync(key, stats);
        LogAct(new { json = savedBytes is null ? null : Encoding.UTF8.GetString(savedBytes), savedOptions });

        // Assert
        JsonSerializer.Deserialize<MaintenanceStatsDto>(savedBytes!).Should().BeEquivalentTo(stats);
        savedOptions!.SlidingExpiration.Should().Be(TimeSpan.FromMinutes(10));
        savedOptions.AbsoluteExpiration.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(60), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SetAsync_ShouldUseTheConfiguredExpirations()
    {
        // Arrange
        var key = Fixture.Create<string>();
        var stats = Fixture.Create<MaintenanceStatsDto>();

        DistributedCacheEntryOptions? savedOptions = null;
        _cacheMock
            .Setup(c => c.SetAsync(key, It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>((_, _, options, _) => savedOptions = options)
            .Returns(Task.CompletedTask);

        var service = CreateService(new Dictionary<string, string?>
        {
            ["Redis:AbsoluteExpiration"] = "5",
            ["Redis:SlidingExpiration"] = "2"
        });

        // Act
        await service.SetAsync(key, stats);
        LogAct(savedOptions);

        // Assert
        savedOptions!.SlidingExpiration.Should().Be(TimeSpan.FromMinutes(2));
        savedOptions.AbsoluteExpiration.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(5), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SetAsync_ShouldUseExplicitExpiryOverridesInsteadOfCacheDefaults()
    {
        DistributedCacheEntryOptions? savedOptions = null;
        _cacheMock.Setup(c => c.SetAsync("verification", It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>((_, _, options, _) => savedOptions = options)
            .Returns(Task.CompletedTask);

        await CreateService().SetAsync("verification", "code-hash", absoluteExpiration: TimeSpan.FromMinutes(5),
            slidingExpiration: TimeSpan.FromMinutes(1));

        savedOptions!.AbsoluteExpiration.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(5), TimeSpan.FromSeconds(2));
        savedOptions.SlidingExpiration.Should().Be(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task RemoveAsync_ShouldRemoveTheKeyFromTheCache()
    {
        // Arrange
        var key = Fixture.Create<string>();
        LogArrange(nameof(key), key);

        _cacheMock.Setup(c => c.RemoveAsync(key, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await CreateService().RemoveAsync(key);

        // Assert
        _cacheMock.Verify(c => c.RemoveAsync(key, It.IsAny<CancellationToken>()), Times.Once);
    }
}