using System.Net;
using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.Locations;
using AabuReports.Core.Enums;
using AabuReports.Core.Results;
using AabuReports.Core.ValueObjects;
using AabuReports.Infrastructure.Caching;
using AabuReports.Infrastructure.Services;
using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit.Abstractions;

namespace AabuReports.Services.UnitTests;

public class CachedLocationServiceTests : ServiceTestBase
{
    private const string AllKey = $"{CacheKeys.Locations}all";

    private readonly ILocationService _cachedService;
    private readonly Mock<ILocationService> _innerMock;
    private readonly Mock<IRedisService> _redisMock;

    public CachedLocationServiceTests(ITestOutputHelper output) : base(output)
    {
        _innerMock = new Mock<ILocationService>();
        _redisMock = new Mock<IRedisService>();

        _cachedService = new CachedLocationService(
            _innerMock.Object,
            _redisMock.Object,
            NullLogger<CachedLocationService>.Instance);
    }

    private static string ByIdKey(Guid id) => $"{CacheKeys.Locations}id:{id}";

    private IReadOnlyList<LocationDto> CreateLocationDtos() => Fixture.CreateMany<LocationDto>(2).ToList();

    private void VerifyCacheCleared(Times times)
    {
        _redisMock.Verify(r => r.RemoveByPrefixAsync(CacheKeys.Locations, It.IsAny<CancellationToken>()), times);
        _redisMock.Verify(r => r.RemoveByPrefixAsync(CacheKeys.Reports, It.IsAny<CancellationToken>()), times);
    }

    #region GetAllAsync

    [Fact]
    public async Task GetAllAsync_ShouldReturnCachedValueWithoutCallingInnerService_WhenCacheHit()
    {
        // Arrange
        var cached = CreateLocationDtos();
        LogArrange(nameof(cached), cached);

        _redisMock.Setup(r => r.GetAsync<IReadOnlyList<LocationDto>>(AllKey, It.IsAny<CancellationToken>())).ReturnsAsync(cached);

        // Act
        var result = await _cachedService.GetAllAsync();
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(cached);

        _innerMock.Verify(i => i.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAllAsync_ShouldCallInnerServiceAndCacheTheResult_WhenCacheMiss()
    {
        // Arrange
        var fromDb = CreateLocationDtos();
        LogArrange(nameof(fromDb), fromDb);

        _redisMock.Setup(r => r.GetAsync<IReadOnlyList<LocationDto>>(AllKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<LocationDto>?)null);
        _innerMock.Setup(i => i.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<LocationDto>>.Success(fromDb));

        // Act
        var result = await _cachedService.GetAllAsync();
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(fromDb);

        _redisMock.Verify(r => r.SetAsync<IReadOnlyList<LocationDto>>(AllKey, fromDb, It.IsAny<CancellationToken>(), null, null), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_ShouldNotCacheFailedResults()
    {
        // Arrange
        _redisMock.Setup(r => r.GetAsync<IReadOnlyList<LocationDto>>(AllKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<LocationDto>?)null);
        _innerMock.Setup(i => i.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<LocationDto>>.Failure("db down", HttpStatusCode.InternalServerError));

        // Act
        var result = await _cachedService.GetAllAsync();
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("db down");

        _redisMock.Verify(
            r => r.SetAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<LocationDto>>(), It.IsAny<CancellationToken>(), null, null), Times.Never);
    }

    [Fact]
    public async Task GetAllAsync_ShouldFallBackToInnerService_WhenRedisReadThrows()
    {
        // Arrange
        var fromDb = CreateLocationDtos();
        LogArrange(nameof(fromDb), fromDb);

        _redisMock.Setup(r => r.GetAsync<IReadOnlyList<LocationDto>>(AllKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis is down"));
        _innerMock.Setup(i => i.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<LocationDto>>.Success(fromDb));

        // Act
        var result = await _cachedService.GetAllAsync();
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(fromDb);
    }

    [Fact]
    public async Task GetAllAsync_ShouldStillReturnTheResult_WhenRedisWriteThrows()
    {
        // Arrange
        var fromDb = CreateLocationDtos();
        LogArrange(nameof(fromDb), fromDb);

        _redisMock.Setup(r => r.GetAsync<IReadOnlyList<LocationDto>>(AllKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<LocationDto>?)null);
        _redisMock.Setup(r => r.SetAsync<IReadOnlyList<LocationDto>>(AllKey, It.IsAny<IReadOnlyList<LocationDto>>(), It.IsAny<CancellationToken>(), null, null))
            .ThrowsAsync(new InvalidOperationException("redis is down"));
        _innerMock.Setup(i => i.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<LocationDto>>.Success(fromDb));

        // Act
        var result = await _cachedService.GetAllAsync();
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(fromDb);
    }

    #endregion

    #region GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_ShouldReturnCachedValueWithoutCallingInnerService_WhenCacheHit()
    {
        // Arrange
        var cached = Fixture.Create<LocationDto>();
        LogArrange(nameof(cached), cached);

        _redisMock.Setup(r => r.GetAsync<LocationDto>(ByIdKey(cached.Id), It.IsAny<CancellationToken>())).ReturnsAsync(cached);

        // Act
        var result = await _cachedService.GetByIdAsync(cached.Id);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(cached);
        _innerMock.Verify(i => i.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldCallInnerServiceAndCacheTheResult_WhenCacheMiss()
    {
        // Arrange
        var fromDb = Fixture.Create<LocationDto>();
        LogArrange(nameof(fromDb), fromDb);

        _redisMock.Setup(r => r.GetAsync<LocationDto>(ByIdKey(fromDb.Id), It.IsAny<CancellationToken>())).ReturnsAsync((LocationDto?)null);
        _innerMock.Setup(i => i.GetByIdAsync(fromDb.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LocationDto>.Success(fromDb));

        // Act
        var result = await _cachedService.GetByIdAsync(fromDb.Id);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _redisMock.Verify(r => r.SetAsync<LocationDto>(ByIdKey(fromDb.Id), fromDb, It.IsAny<CancellationToken>(), null, null), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldNotCacheNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();

        _redisMock.Setup(r => r.GetAsync<LocationDto>(ByIdKey(id), It.IsAny<CancellationToken>())).ReturnsAsync((LocationDto?)null);
        _innerMock.Setup(i => i.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LocationDto>.Failure("Location not found", HttpStatusCode.NotFound));

        // Act
        var result = await _cachedService.GetByIdAsync(id);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
        _redisMock.Verify(r => r.SetAsync(It.IsAny<string>(), It.IsAny<LocationDto>(), It.IsAny<CancellationToken>(), null, null), Times.Never);
    }

    #endregion

    #region Writes

    [Fact]
    public async Task CreateAsync_ShouldClearLocationsAndReportsCache_WhenSuccessful()
    {
        // Arrange
        var dto = new LocationCreateDto { Title = "Library", Coordinates = new MapCoordinates { Lat = 1, Lng = 2 }, Type = LocationType.Building };
        _innerMock.Setup(i => i.CreateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LocationDto>.Success(Fixture.Create<LocationDto>(), HttpStatusCode.Created));

        // Act
        var result = await _cachedService.CreateAsync(dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyCacheCleared(Times.Once());
    }

    [Fact]
    public async Task CreateAsync_ShouldNotClearCache_WhenInnerServiceFails()
    {
        // Arrange
        var dto = new LocationCreateDto { Title = "Library", Coordinates = new MapCoordinates { Lat = 1, Lng = 2 }, Type = LocationType.Building };
        _innerMock.Setup(i => i.CreateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LocationDto>.Failure("boom", HttpStatusCode.InternalServerError));

        // Act
        var result = await _cachedService.CreateAsync(dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        VerifyCacheCleared(Times.Never());
    }

    [Fact]
    public async Task UpdateAsync_ShouldClearLocationsAndReportsCache_WhenSuccessful()
    {
        // Arrange
        var dto = new LocationUpdateDto { Id = Guid.NewGuid(), Title = "New title" };
        _innerMock.Setup(i => i.UpdateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LocationDto>.Success(Fixture.Create<LocationDto>()));

        // Act
        var result = await _cachedService.UpdateAsync(dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyCacheCleared(Times.Once());
    }

    [Fact]
    public async Task UpdateAsync_ShouldNotClearCache_WhenInnerServiceFails()
    {
        // Arrange
        var dto = new LocationUpdateDto { Id = Guid.NewGuid(), Title = "New title" };
        _innerMock.Setup(i => i.UpdateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LocationDto>.Failure("Location not found", HttpStatusCode.NotFound));

        // Act
        var result = await _cachedService.UpdateAsync(dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        VerifyCacheCleared(Times.Never());
    }

    [Fact]
    public async Task DeleteAsync_ShouldClearLocationsAndReportsCache_WhenSuccessful()
    {
        // Arrange
        var id = Guid.NewGuid();
        _innerMock.Setup(i => i.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(HttpStatusCode.NoContent));

        // Act
        var result = await _cachedService.DeleteAsync(id);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyCacheCleared(Times.Once());
    }

    [Fact]
    public async Task DeleteAsync_ShouldNotClearCache_WhenInnerServiceFails()
    {
        // Arrange
        var id = Guid.NewGuid();
        _innerMock.Setup(i => i.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("Location not found", HttpStatusCode.NotFound));

        // Act
        var result = await _cachedService.DeleteAsync(id);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        VerifyCacheCleared(Times.Never());
    }

    [Fact]
    public async Task DeleteAsync_ShouldStillReturnTheResult_WhenClearingTheCacheThrows()
    {
        // Arrange
        var id = Guid.NewGuid();
        _innerMock.Setup(i => i.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(HttpStatusCode.NoContent));
        _redisMock.Setup(r => r.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis is down"));

        // Act
        var result = await _cachedService.DeleteAsync(id);
        LogAct(result);

        // Assert: the DB already changed, a cache problem must not fail the request
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // both prefixes are still tried even though the first one threw
        _redisMock.Verify(r => r.RemoveByPrefixAsync(CacheKeys.Locations, It.IsAny<CancellationToken>()), Times.Once);
        _redisMock.Verify(r => r.RemoveByPrefixAsync(CacheKeys.Reports, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}