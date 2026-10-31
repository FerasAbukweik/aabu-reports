using AabuReports.Core.Dtos.Locations;
using AabuReports.Core.Entities;
using AabuReports.Core.Enums;
using AabuReports.Core.ValueObjects;
using AabuReports.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace AabuReports.Repository.UnitTests;

public class LocationRepositoryTests : RepositoryTestBase
{
    private readonly LocationRepository _repository;

    public LocationRepositoryTests(ITestOutputHelper output) : base(output)
    {
        _repository = new LocationRepository(_dbContext);
    }

    #region AddLocation

    [Fact]
    public async Task AddLocation_ShouldPersistTheLocationWithItsCoordinates_WhenChangesAreSaved()
    {
        // Arrange
        var location = new Location
        {
            Title = "Library",
            Coordinates = new MapCoordinates { Lat = 32.1234, Lng = 36.5678 },
            Type = LocationType.Building
        };
        LogArrange(nameof(location), location);

        // Act
        _repository.AddLocation(location);
        var saved = await _repository.SaveChangesAsync();
        LogAct(saved);

        // Assert
        saved.Should().BeTrue();

        _dbContext.ChangeTracker.Clear();
        var stored = await _dbContext.Locations.SingleAsync(l => l.Id == location.Id);
        stored.Title.Should().Be("Library");
        stored.Type.Should().Be(LocationType.Building);
        stored.Coordinates.Lat.Should().Be(32.1234);
        stored.Coordinates.Lng.Should().Be(36.5678);
    }

    [Fact]
    public async Task AddLocation_ShouldThrow_WhenTheCoordinatesAlreadyExist()
    {
        // Arrange
        var existing = await SeedLocationAsync();
        LogArrange(nameof(existing), existing);

        _repository.AddLocation(new Location
        {
            Title = "Another title",
            Coordinates = new MapCoordinates { Lat = existing.Coordinates.Lat, Lng = existing.Coordinates.Lng },
            Type = LocationType.Pin
        });

        // Act
        var act = () => _repository.SaveChangesAsync();
        LogAct("expecting the save to throw");

        // Assert: (Lat, Lng) has a unique index
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    #endregion

    #region GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenLocationDoesNotExist()
    {
        var id = Guid.NewGuid();
        LogArrange(nameof(id), id);

        // Act
        var result = await _repository.GetByIdAsync(id);
        LogAct(result);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNormalStateAndZeroCount_WhenLocationHasNoReports()
    {
        // Arrange
        var location = await SeedLocationAsync();

        LogArrange(nameof(location), location);

        // Act
        var result = await _repository.GetByIdAsync(location.Id);
        LogAct(result);

        // Assert
        result.Should().NotBeNull();
        result!.State.Should().Be(ReportStateEnum.Normal);
        result.ActiveReportsCount.Should().Be(0);
        _dbContext.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnTheHighestReportStateAndTheNumberOfReports()
    {
        // Arrange
        var user = await SeedUserAsync();
        var location = await SeedLocationAsync();
        var other = await SeedLocationAsync();
        await SeedReportAsync(user.Id, location.Id, ReportStateEnum.Fixed);
        await SeedReportAsync(user.Id, location.Id, ReportStateEnum.Pending);
        await SeedReportAsync(user.Id, location.Id, ReportStateEnum.InProgress);
        await SeedReportAsync(user.Id, other.Id, ReportStateEnum.NeedsFollowUp); // another location, must not count

        LogArrange("arranged data", new { user, location, other });

        // Act
        var result = await _repository.GetByIdAsync(location.Id);
        LogAct(result);

        // Assert: Pending is the most urgent of Fixed / Pending / InProgress
        result!.State.Should().Be(ReportStateEnum.Pending);
        result.ActiveReportsCount.Should().Be(2);
    }

    #endregion

    #region GetAllAsync

    [Fact]
    public async Task GetAllAsync_ShouldReturnBuildingsAndOnlyThePinsThatAreActiveOrRecentlyFixed_OrderedByTitle()
    {
        // Arrange
        var user = await SeedUserAsync();

        var buildingB = await SeedLocationAsync("B Building", LocationType.Building); // no reports
        var buildingA = await SeedLocationAsync("A Building", LocationType.Building);
        await SeedReportAsync(user.Id, buildingA.Id, ReportStateEnum.Fixed, fixDate: DateTime.UtcNow.AddDays(-30));

        var pinActive = await SeedLocationAsync("Pin active", LocationType.Pin);
        await SeedReportAsync(user.Id, pinActive.Id, ReportStateEnum.Pending);

        var pinFixedRecently = await SeedLocationAsync("Pin fixed recently", LocationType.Pin);
        await SeedReportAsync(user.Id, pinFixedRecently.Id, ReportStateEnum.Fixed, fixDate: DateTime.UtcNow.AddDays(-1));

        var pinFixedLongAgo = await SeedLocationAsync("Pin fixed long ago", LocationType.Pin);
        await SeedReportAsync(user.Id, pinFixedLongAgo.Id, ReportStateEnum.Fixed, fixDate: DateTime.UtcNow.AddDays(-30));

        await SeedLocationAsync("Pin without reports", LocationType.Pin);

        LogArrange("arranged data", new { user, buildingB, buildingA, pinActive, pinFixedRecently, pinFixedLongAgo });

        // Act
        var result = await _repository.GetAllAsync();
        LogAct(result);

        // Assert
        result.Select(l => l.Title).Should().Equal("A Building", "B Building", "Pin active", "Pin fixed recently");

        var a = result.Single(l => l.Id == buildingA.Id);
        a.State.Should().Be(ReportStateEnum.Fixed);
        a.ActiveReportsCount.Should().Be(0); // fixed reports are not active

        var b = result.Single(l => l.Id == buildingB.Id);
        b.State.Should().Be(ReportStateEnum.Normal);
        b.ActiveReportsCount.Should().Be(0);

        var active = result.Single(l => l.Id == pinActive.Id);
        active.State.Should().Be(ReportStateEnum.Pending);
        active.ActiveReportsCount.Should().Be(1);

        result.Single(l => l.Id == pinFixedRecently.Id).ActiveReportsCount.Should().Be(0);
        _dbContext.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnEmpty_WhenThereAreNoLocations()
    {
        LogArrange("locations in the database", 0);

        // Act
        var result = await _repository.GetAllAsync();
        LogAct(result);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Update

    [Fact]
    public async Task Update_ShouldReturnNull_WhenLocationDoesNotExist()
    {
        var dto = new LocationUpdateDto { Id = Guid.NewGuid(), Title = "x" };
        LogArrange(nameof(dto), dto);

        // Act
        var result = await _repository.Update(dto);
        LogAct(result);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Update_ShouldChangeOnlyTheGivenFields()
    {
        // Arrange
        var location = await SeedLocationAsync("Old title", LocationType.Building);
        var dto = new LocationUpdateDto { Id = location.Id, Title = "New title" };
        LogArrange(nameof(dto), dto);

        // Act
        var updated = await _repository.Update(dto);
        var saved = await _repository.SaveChangesAsync();
        LogAct(updated);

        // Assert
        saved.Should().BeTrue();

        _dbContext.ChangeTracker.Clear();
        var stored = await _dbContext.Locations.SingleAsync(l => l.Id == location.Id);
        stored.Title.Should().Be("New title");
        stored.Type.Should().Be(LocationType.Building);
        stored.Coordinates.Lat.Should().Be(location.Coordinates.Lat);
        stored.Coordinates.Lng.Should().Be(location.Coordinates.Lng);
    }

    [Fact]
    public async Task Update_ShouldChangeTitleTypeAndCoordinates_WhenAllAreGiven()
    {
        // Arrange
        var location = await SeedLocationAsync("Old title", LocationType.Building);
        var dto = new LocationUpdateDto
        {
            Id = location.Id,
            Title = "New title",
            Type = LocationType.Pin,
            Coordinates = new MapCoordinates { Lat = 10.5, Lng = 20.5 }
        };
        LogArrange(nameof(dto), dto);

        // Act
        await _repository.Update(dto);
        var saved = await _repository.SaveChangesAsync();
        LogAct(saved);

        // Assert
        saved.Should().BeTrue();

        _dbContext.ChangeTracker.Clear();
        var stored = await _dbContext.Locations.SingleAsync(l => l.Id == location.Id);
        stored.Title.Should().Be("New title");
        stored.Type.Should().Be(LocationType.Pin);
        stored.Coordinates.Lat.Should().Be(10.5);
        stored.Coordinates.Lng.Should().Be(20.5);
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldReturnFalse_WhenUpdateChangesNothing()
    {
        // Arrange
        var location = await SeedLocationAsync("Same title");

        LogArrange(nameof(location), location);

        // Act
        await _repository.Update(new LocationUpdateDto { Id = location.Id, Title = "Same title" });
        var saved = await _repository.SaveChangesAsync();
        LogAct(saved);

        // Assert
        saved.Should().BeFalse();
    }

    #endregion

    #region RemoveById

    [Fact]
    public async Task RemoveById_ShouldReturnNull_WhenLocationDoesNotExist()
    {
        var id = Guid.NewGuid();
        LogArrange(nameof(id), id);

        // Act
        var result = await _repository.RemoveById(id);
        LogAct(result);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveById_ShouldDeleteTheLocationAndItsReports_WhenChangesAreSaved()
    {
        // Arrange
        var user = await SeedUserAsync();
        var location = await SeedLocationAsync();
        var keptLocation = await SeedLocationAsync();
        var report = await SeedReportAsync(user.Id, location.Id);
        var keptReport = await SeedReportAsync(user.Id, keptLocation.Id);

        LogArrange("arranged data", new { user, location, keptLocation, report, keptReport });

        // Act
        var removed = await _repository.RemoveById(location.Id);
        var saved = await _repository.SaveChangesAsync();
        LogAct(removed);

        // Assert
        removed.Should().NotBeNull();
        saved.Should().BeTrue();

        _dbContext.ChangeTracker.Clear();
        (await _dbContext.Locations.AnyAsync(l => l.Id == location.Id)).Should().BeFalse();
        (await _dbContext.Reports.AnyAsync(r => r.Id == report.Id)).Should().BeFalse();
        (await _dbContext.Locations.AnyAsync(l => l.Id == keptLocation.Id)).Should().BeTrue();
        (await _dbContext.Reports.AnyAsync(r => r.Id == keptReport.Id)).Should().BeTrue();
    }

    #endregion
}