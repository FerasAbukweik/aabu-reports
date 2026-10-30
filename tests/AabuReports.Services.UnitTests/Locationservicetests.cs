using System.Net;
using AabuReports.Core.Abstractions.Repositories;
using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.Locations;
using AabuReports.Core.Entities;
using AabuReports.Core.Enums;
using AabuReports.Core.ValueObjects;
using AabuReports.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit.Abstractions;

namespace AabuReports.Services.UnitTests;

public class LocationServiceTests : ServiceTestBase
{
    private readonly ILocationService _locationService;
    private readonly Mock<ILocationRepository> _repositoryMock;

    public LocationServiceTests(ITestOutputHelper output) : base(output)
    {
        _repositoryMock = new Mock<ILocationRepository>();
        _locationService = new LocationService(_repositoryMock.Object, NullLogger<LocationService>.Instance);
    }

    #region GetAllAsync

    [Fact]
    public async Task GetAllAsync_ShouldReturnMappedLocations()
    {
        // Arrange
        IReadOnlyList<Location> locations = new List<Location>
        {
            CreateLocation(LocationType.Building, ReportStateEnum.Pending, 3),
            CreateLocation(LocationType.Pin, ReportStateEnum.Fixed, 1)
        };
        LogArrange(nameof(locations), locations);

        _repositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(locations);

        // Act
        var result = await _locationService.GetAllAsync();
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value![0].Id.Should().Be(locations[0].Id);
        result.Value[0].Status.Should().Be(ReportStateEnum.Pending);
        result.Value[0].ActiveReportsCount.Should().Be(3);
        result.Value[1].Title.Should().Be(locations[1].Title);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnEmptyList_WhenThereAreNoLocations()
    {
        // Arrange
        IReadOnlyList<Location> locations = new List<Location>();
        _repositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(locations);

        // Act
        var result = await _locationService.GetAllAsync();
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    #endregion

    #region GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNotFound_WhenLocationDoesNotExist()
    {
        // Arrange
        var id = Guid.NewGuid();
        LogArrange(nameof(id), id);

        _repositoryMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Location?)null);

        // Act
        var result = await _locationService.GetByIdAsync(id);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
        result.ErrorMessage.Should().Be("Location not found");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnMappedLocation_WhenLocationExists()
    {
        // Arrange
        var location = CreateLocation(LocationType.Building, ReportStateEnum.InProgress, 2);
        LogArrange(nameof(location), location);

        _repositoryMock.Setup(r => r.GetByIdAsync(location.Id, It.IsAny<CancellationToken>())).ReturnsAsync(location);

        // Act
        var result = await _locationService.GetByIdAsync(location.Id);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(location.Id);
        result.Value.Title.Should().Be(location.Title);
        result.Value.Status.Should().Be(ReportStateEnum.InProgress);
        result.Value.ActiveReportsCount.Should().Be(2);
    }

    #endregion

    #region CreateAsync

    private static LocationCreateDto CreateLocationDto(LocationType type) => new()
    {
        Title = "  Library  ",
        Coordinates = new MapCoordinates { Lat = 32.1, Lng = 36.2 },
        Type = type
    };

    [Fact]
    public async Task CreateAsync_ShouldReturnInternalServerError_WhenNothingWasSaved()
    {
        // Arrange
        var dto = CreateLocationDto(LocationType.Building);
        LogArrange(nameof(dto), dto);

        _repositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var result = await _locationService.CreateAsync(dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        result.ErrorMessage.Should().Be("no changes where applied to the DB");
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnCreatedBuildingWithNormalStatus_WhenSaveSucceeds()
    {
        // Arrange
        var dto = CreateLocationDto(LocationType.Building);
        LogArrange(nameof(dto), dto);

        Location? added = null;
        _repositoryMock.Setup(r => r.AddLocation(It.IsAny<Location>())).Callback<Location>(l => added = l);
        _repositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await _locationService.CreateAsync(dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.Created);

        added.Should().NotBeNull();
        added!.Title.Should().Be("Library"); // trimmed
        added.Coordinates.Lat.Should().Be(32.1);
        added.Coordinates.Lng.Should().Be(36.2);

        result.Value!.Id.Should().Be(added.Id);
        result.Value.Title.Should().Be("Library");
        result.Value.Status.Should().Be(ReportStateEnum.Normal);
        result.Value.ActiveReportsCount.Should().Be(0);
    }

    /// <summary>
    /// KNOWN BUG: this test fails against the current code. LocationService.CreateAsync only sets State / ActiveReportsCount
    /// for Building, so for a Pin Location.ToDto() throws "State or report count is null".
    /// Fix: move the two assignments out of the "if (Type == Building)" block (a new location always has no reports).
    /// </summary>
    [Fact]
    public async Task CreateAsync_ShouldReturnCreated_WhenTypeIsPin()
    {
        // Arrange
        var dto = CreateLocationDto(LocationType.Pin);
        LogArrange(nameof(dto), dto);

        _repositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await _locationService.CreateAsync(dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Value!.Type.Should().Be(LocationType.Pin);
    }

    #endregion

    #region UpdateAsync

    [Fact]
    public async Task UpdateAsync_ShouldReturnNotFound_WhenLocationDoesNotExist()
    {
        // Arrange
        var dto = new LocationUpdateDto { Id = Guid.NewGuid(), Title = "New title" };
        LogArrange(nameof(dto), dto);

        _repositoryMock.Setup(r => r.Update(dto, It.IsAny<CancellationToken>())).ReturnsAsync((Location?)null);

        // Act
        var result = await _locationService.UpdateAsync(dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
        result.ErrorMessage.Should().Be("Location not found");
    }

    [Fact]
    public async Task UpdateAsync_ShouldReturnInternalServerError_WhenNothingWasSaved()
    {
        // Arrange
        var location = CreateLocation();
        var dto = new LocationUpdateDto { Id = location.Id, Title = "New title" };
        LogArrange(nameof(dto), dto);

        _repositoryMock.Setup(r => r.Update(dto, It.IsAny<CancellationToken>())).ReturnsAsync(location);
        _repositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var result = await _locationService.UpdateAsync(dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task UpdateAsync_ShouldReturnUpdatedLocation_WhenSaveSucceeds()
    {
        // Arrange
        var location = CreateLocation(LocationType.Building, ReportStateEnum.Pending, 4);
        var dto = new LocationUpdateDto { Id = location.Id, Title = "New title" };
        location.Title = "New title";
        LogArrange(nameof(dto), dto);

        _repositoryMock.Setup(r => r.Update(dto, It.IsAny<CancellationToken>())).ReturnsAsync(location);
        _repositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await _locationService.UpdateAsync(dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(location.Id);
        result.Value.Title.Should().Be("New title");
    }

    #endregion

    #region DeleteAsync

    [Fact]
    public async Task DeleteAsync_ShouldReturnNotFound_WhenLocationDoesNotExist()
    {
        // Arrange
        var id = Guid.NewGuid();
        LogArrange(nameof(id), id);

        _repositoryMock.Setup(r => r.RemoveById(id, It.IsAny<CancellationToken>())).ReturnsAsync((Location?)null);

        // Act
        var result = await _locationService.DeleteAsync(id);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
        _repositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnInternalServerError_WhenNothingWasSaved()
    {
        // Arrange
        var location = CreateLocation();
        LogArrange(nameof(location), location);

        _repositoryMock.Setup(r => r.RemoveById(location.Id, It.IsAny<CancellationToken>())).ReturnsAsync(location);
        _repositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var result = await _locationService.DeleteAsync(location.Id);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnNoContent_WhenLocationIsDeleted()
    {
        // Arrange
        var location = CreateLocation();
        LogArrange(nameof(location), location);

        _repositoryMock.Setup(r => r.RemoveById(location.Id, It.IsAny<CancellationToken>())).ReturnsAsync(location);
        _repositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await _locationService.DeleteAsync(location.Id);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    #endregion
}