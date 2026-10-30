using System.Net;
using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.LazyLoading;
using AabuReports.Core.Dtos.Reports;
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

public class CachedReportServiceTests : ServiceTestBase
{
    private const string MaintenanceStatsKey = $"{CacheKeys.Reports}stats:maintenance";

    private readonly IReportService _cachedService;
    private readonly Mock<IReportService> _innerMock;
    private readonly Mock<IRedisService> _redisMock;

    public CachedReportServiceTests(ITestOutputHelper output) : base(output)
    {
        _innerMock = new Mock<IReportService>();
        _redisMock = new Mock<IRedisService>();

        _cachedService = new CachedReportService(
            _innerMock.Object,
            _redisMock.Object,
            NullLogger<CachedReportService>.Instance);
    }

    private static string MapKey(Guid locationId, LazyLoadingDto lazy) => $"{CacheKeys.Reports}map:{locationId}:{lazy.Skip}:{lazy.Take}";
    private static string HistoryKey(Guid? userId) => $"{CacheKeys.Reports}stats:history:{(userId?.ToString() ?? "all")}";

    private void VerifyCacheCleared(Times times)
    {
        _redisMock.Verify(r => r.RemoveByPrefixAsync(CacheKeys.Reports, It.IsAny<CancellationToken>()), times);
        _redisMock.Verify(r => r.RemoveByPrefixAsync(CacheKeys.Locations, It.IsAny<CancellationToken>()), times);
    }

    #region cached reads

    [Fact]
    public async Task LazyGetMapReportsAsync_ShouldReturnCachedValue_WhenCacheHit()
    {
        // Arrange
        var locationId = Guid.NewGuid();
        var lazy = new LazyLoadingDto { Skip = 0, Take = 20 };
        IReadOnlyList<ReportMapDto> cached = Fixture.CreateMany<ReportMapDto>(2).ToList();
        LogArrange(nameof(cached), cached);

        _redisMock.Setup(r => r.GetAsync<IReadOnlyList<ReportMapDto>>(MapKey(locationId, lazy), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);

        // Act
        var result = await _cachedService.LazyGetMapReportsAsync(locationId, lazy);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(cached);
        _innerMock.Verify(
            i => i.LazyGetMapReportsAsync(It.IsAny<Guid>(), It.IsAny<LazyLoadingDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LazyGetMapReportsAsync_ShouldCallInnerServiceAndCache_WhenCacheMiss()
    {
        // Arrange
        var locationId = Guid.NewGuid();
        var lazy = new LazyLoadingDto { Skip = 40, Take = 20 };
        IReadOnlyList<ReportMapDto> fromDb = Fixture.CreateMany<ReportMapDto>(2).ToList();
        LogArrange(nameof(fromDb), fromDb);

        _redisMock.Setup(r => r.GetAsync<IReadOnlyList<ReportMapDto>>(MapKey(locationId, lazy), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ReportMapDto>?)null);
        _innerMock.Setup(i => i.LazyGetMapReportsAsync(locationId, lazy, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<ReportMapDto>>.Success(fromDb));

        // Act
        var result = await _cachedService.LazyGetMapReportsAsync(locationId, lazy);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _redisMock.Verify(
            r => r.SetAsync<IReadOnlyList<ReportMapDto>>(MapKey(locationId, lazy), fromDb, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMaintenanceStatsAsync_ShouldReturnCachedValue_WhenCacheHit()
    {
        // Arrange
        var cached = Fixture.Create<MaintenanceStatsDto>();
        LogArrange(nameof(cached), cached);

        _redisMock.Setup(r => r.GetAsync<MaintenanceStatsDto>(MaintenanceStatsKey, It.IsAny<CancellationToken>())).ReturnsAsync(cached);

        // Act
        var result = await _cachedService.GetMaintenanceStatsAsync();
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(cached);
        _innerMock.Verify(i => i.GetMaintenanceStatsAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetMaintenanceStatsAsync_ShouldCallInnerServiceAndCache_WhenCacheMiss()
    {
        // Arrange
        var fromDb = Fixture.Create<MaintenanceStatsDto>();
        LogArrange(nameof(fromDb), fromDb);

        _redisMock.Setup(r => r.GetAsync<MaintenanceStatsDto>(MaintenanceStatsKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MaintenanceStatsDto?)null);
        _innerMock.Setup(i => i.GetMaintenanceStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MaintenanceStatsDto>.Success(fromDb));

        // Act
        var result = await _cachedService.GetMaintenanceStatsAsync();
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _redisMock.Verify(r => r.SetAsync<MaintenanceStatsDto>(MaintenanceStatsKey, fromDb, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetHistoryStatsAsync_ShouldUseADifferentKeyPerUser_AndCacheOnMiss(bool forOneUser)
    {
        // Arrange
        Guid? userId = forOneUser ? Guid.NewGuid() : null;
        var fromDb = Fixture.Create<ReportHistoryStatsDto>();
        LogArrange(nameof(userId), userId);

        _redisMock.Setup(r => r.GetAsync<ReportHistoryStatsDto>(HistoryKey(userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReportHistoryStatsDto?)null);
        _innerMock.Setup(i => i.GetHistoryStatsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ReportHistoryStatsDto>.Success(fromDb));

        // Act
        var result = await _cachedService.GetHistoryStatsAsync(userId);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _redisMock.Verify(r => r.SetAsync<ReportHistoryStatsDto>(HistoryKey(userId), fromDb, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetHistoryStatsAsync_ShouldReturnCachedValue_WhenCacheHit()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var cached = Fixture.Create<ReportHistoryStatsDto>();
        LogArrange(nameof(cached), cached);

        _redisMock.Setup(r => r.GetAsync<ReportHistoryStatsDto>(HistoryKey(userId), It.IsAny<CancellationToken>())).ReturnsAsync(cached);

        // Act
        var result = await _cachedService.GetHistoryStatsAsync(userId);
        LogAct(result);

        // Assert
        result.Value.Should().BeSameAs(cached);
        _innerMock.Verify(i => i.GetHistoryStatsAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region reads that always hit the database

    [Fact]
    public async Task LazyGetFilteredAsync_ShouldAlwaysCallInnerServiceAndNeverUseRedis()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var filter = new ReportFilterDto();
        var lazy = new LazyLoadingDto { Skip = 0, Take = 10 };
        IReadOnlyList<ReportDto> reports = new List<ReportDto> { CreateReport().ToDto() };

        _innerMock.Setup(i => i.LazyGetFilteredAsync(userId, filter, lazy, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<ReportDto>>.Success(reports));

        // Act
        var result = await _cachedService.LazyGetFilteredAsync(userId, filter, lazy);
        LogAct(result);

        // Assert
        result.Value.Should().BeSameAs(reports);
        _redisMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldAlwaysCallInnerServiceAndNeverUseRedis()
    {
        // Arrange
        var dto = CreateReport().ToDto();
        _innerMock.Setup(i => i.GetByIdAsync("968FCB28", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ReportDto>.Success(dto));

        // Act
        var result = await _cachedService.GetByIdAsync("968FCB28");
        LogAct(result);

        // Assert
        result.Value.Should().BeSameAs(dto);
        _redisMock.VerifyNoOtherCalls();
    }

    #endregion

    #region writes

    private ReportCreateForLocationDto CreateForLocationDto() => new()
    {
        LocationId = Guid.NewGuid(),
        Category = ReportCategory.Cleaning,
        Severity = ReportSeverity.Small,
        Description = Fixture.Create<string>()
    };

    private ReportCreateForCoordinatesDto CreateForCoordinatesDto() => new()
    {
        Coordinates = new MapCoordinates { Lat = 1, Lng = 2 },
        Category = ReportCategory.Cleaning,
        Severity = ReportSeverity.Small,
        Description = Fixture.Create<string>()
    };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateForLocationIdAsync_ShouldClearCacheOnlyOnSuccess(bool succeeds)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var dto = CreateForLocationDto();
        LogArrange(nameof(dto), dto);

        _innerMock.Setup(i => i.CreateForLocationIdAsync(userId, dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(succeeds
                ? Result<ReportCreateResponseDto>.Success(Fixture.Create<ReportCreateResponseDto>(), HttpStatusCode.Created)
                : Result<ReportCreateResponseDto>.Failure("Location not found", HttpStatusCode.NotFound));

        // Act
        var result = await _cachedService.CreateForLocationIdAsync(userId, dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().Be(succeeds);
        VerifyCacheCleared(succeeds ? Times.Once() : Times.Never());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateForCoordinates_ShouldClearCacheOnlyOnSuccess(bool succeeds)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var dto = CreateForCoordinatesDto();
        LogArrange(nameof(dto), dto);

        _innerMock.Setup(i => i.CreateForCoordinates(userId, dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(succeeds
                ? Result<ReportCreateResponseDto>.Success(Fixture.Create<ReportCreateResponseDto>(), HttpStatusCode.Created)
                : Result<ReportCreateResponseDto>.Failure("boom", HttpStatusCode.InternalServerError));

        // Act
        var result = await _cachedService.CreateForCoordinates(userId, dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().Be(succeeds);
        VerifyCacheCleared(succeeds ? Times.Once() : Times.Never());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateStateAsync_ShouldClearCacheOnlyOnSuccess(bool succeeds)
    {
        // Arrange
        var dto = new ReportUpdateStateDto { Id = Guid.NewGuid(), State = ReportStateEnum.Fixed };
        LogArrange(nameof(dto), dto);

        _innerMock.Setup(i => i.UpdateStateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(succeeds
                ? Result<ReportDto>.Success(CreateReport(ReportStateEnum.Fixed).ToDto())
                : Result<ReportDto>.Failure("Report not found", HttpStatusCode.NotFound));

        // Act
        var result = await _cachedService.UpdateStateAsync(dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().Be(succeeds);
        VerifyCacheCleared(succeeds ? Times.Once() : Times.Never());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RateAsync_ShouldClearCacheOnlyOnSuccess(bool succeeds)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var dto = new ReportRateDto { Id = Guid.NewGuid(), Rate = 4 };
        LogArrange(nameof(dto), dto);

        _innerMock.Setup(i => i.RateAsync(userId, dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(succeeds
                ? Result<ReportDto>.Success(CreateReport(ReportStateEnum.Fixed).ToDto())
                : Result<ReportDto>.Failure("A report can only be rated after it is fixed"));

        // Act
        var result = await _cachedService.RateAsync(userId, dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().Be(succeeds);
        VerifyCacheCleared(succeeds ? Times.Once() : Times.Never());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeleteAsync_ShouldClearCacheOnlyOnSuccess(bool succeeds)
    {
        // Arrange
        var id = Guid.NewGuid();
        LogArrange(nameof(id), id);

        _innerMock.Setup(i => i.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(succeeds
                ? Result.Success(HttpStatusCode.NoContent)
                : Result.Failure("Report not found", HttpStatusCode.NotFound));

        // Act
        var result = await _cachedService.DeleteAsync(id);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().Be(succeeds);
        VerifyCacheCleared(succeeds ? Times.Once() : Times.Never());
    }

    #endregion
}