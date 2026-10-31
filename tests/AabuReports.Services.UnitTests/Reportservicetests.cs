using System.Linq.Expressions;
using System.Net;
using AabuReports.Core.Abstractions.Repositories;
using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.Images;
using AabuReports.Core.Dtos.LazyLoading;
using AabuReports.Core.Dtos.Notifications;
using AabuReports.Core.Dtos.Reports;
using AabuReports.Core.Entities;
using AabuReports.Core.Enums;
using AabuReports.Core.Results;
using AabuReports.Core.ValueObjects;
using AabuReports.Infrastructure.Services;
using AutoFixture;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit.Abstractions;

namespace AabuReports.Services.UnitTests;

public class ReportServiceTests : ServiceTestBase
{
    private readonly IReportService _reportService;
    private readonly Mock<IReportRepository> _reportRepositoryMock;
    private readonly Mock<ILocationRepository> _locationRepositoryMock;
    private readonly Mock<IImageService> _imageServiceMock;
    private readonly Mock<INotificationService> _notificationServiceMock;

    public ReportServiceTests(ITestOutputHelper output) : base(output)
    {
        _reportRepositoryMock = new Mock<IReportRepository>();
        _locationRepositoryMock = new Mock<ILocationRepository>();
        _imageServiceMock = new Mock<IImageService>();
        _notificationServiceMock = new Mock<INotificationService>();

        _reportService = new ReportService(
            _reportRepositoryMock.Object,
            _locationRepositoryMock.Object,
            _imageServiceMock.Object,
            _notificationServiceMock.Object,
            NullLogger<ReportService>.Instance);
    }

    // ---------- helpers ----------

    /// <summary> makes FilterAsync return the reports, and hands the predicate it was called with to the test </summary>
    private void SetupFilter(IReadOnlyList<Report> reports, Action<Expression<Func<Report, bool>>>? onPredicate = null)
    {
        _reportRepositoryMock
            .Setup(r => r.FilterAsync(
                It.IsAny<Expression<Func<Report, bool>>>(),
                It.IsAny<Expression<Func<Report, object?>>[]?>(),
                It.IsAny<Expression<Func<Report, object?>>?>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .Callback(new InvocationAction(i => onPredicate?.Invoke((Expression<Func<Report, bool>>)i.Arguments[0])))
            .ReturnsAsync(reports);
    }

    private void SetupSave(bool saved)
    {
        _reportRepositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(saved);
    }

    private void SetupNotification(Result<NotificationDto> result)
    {
        _notificationServiceMock
            .Setup(n => n.CreateAsync(
                It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }

    private ReportCreateForLocationDto CreateForLocationDto(Guid locationId, List<IFormFile>? images = null) => new()
    {
        LocationId = locationId,
        Category = ReportCategory.Plumbing,
        Severity = ReportSeverity.Large,
        Description = $"  {Fixture.Create<string>()}  ",
        Images = images ?? new List<IFormFile>()
    };

    private ReportCreateForCoordinatesDto CreateForCoordinatesDto(List<IFormFile>? images = null) => new()
    {
        Coordinates = new MapCoordinates { Lat = 32.55, Lng = 35.85 },
        Category = ReportCategory.Roads,
        Severity = ReportSeverity.Small,
        Description = $"  {Fixture.Create<string>()}  ",
        Images = images ?? new List<IFormFile>()
    };

    // ---------- LazyGetFilteredAsync ----------

    #region LazyGetFilteredAsync

    [Fact]
    public async Task LazyGetFilteredAsync_ShouldReturnMappedReportsAndPassPaging()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var lazy = new LazyLoadingDto { Skip = 10, Take = 5 };
        IReadOnlyList<Report> reports = new List<Report> { CreateReport(userId: userId), CreateReport(userId: userId) };
        LogArrange(nameof(reports), reports);

        SetupFilter(reports);

        // Act
        var result = await _reportService.LazyGetFilteredAsync(userId, new ReportFilterDto(), lazy);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value!.Select(r => r.Id).Should().Equal(reports.Select(r => r.Id));

        _reportRepositoryMock.Verify(
            r => r.FilterAsync(
                It.IsAny<Expression<Func<Report, bool>>>(),
                It.IsAny<Expression<Func<Report, object?>>[]?>(),
                It.IsAny<Expression<Func<Report, object?>>?>(),
                true, 10, 5, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LazyGetFilteredAsync_ShouldOnlyMatchTheUsersReports_WhenUserIdIsGiven()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var mine = CreateReport(userId: userId);
        var theirs = CreateReport(userId: Guid.NewGuid());

        Expression<Func<Report, bool>>? predicate = null;
        SetupFilter(new List<Report>(), p => predicate = p);

        // Act
        await _reportService.LazyGetFilteredAsync(userId, new ReportFilterDto(), new LazyLoadingDto { Skip = 0, Take = 10 });

        // Assert
        var compiled = predicate!.Compile();
        compiled(mine).Should().BeTrue();
        compiled(theirs).Should().BeFalse();
    }

    [Fact]
    public async Task LazyGetFilteredAsync_ShouldMatchEveryonesReports_WhenUserIdIsNull()
    {
        // Arrange
        var first = CreateReport(userId: Guid.NewGuid());
        var second = CreateReport(userId: Guid.NewGuid());

        Expression<Func<Report, bool>>? predicate = null;
        SetupFilter(new List<Report>(), p => predicate = p);

        // Act
        await _reportService.LazyGetFilteredAsync(null, new ReportFilterDto(), new LazyLoadingDto { Skip = 0, Take = 10 });

        // Assert
        var compiled = predicate!.Compile();
        compiled(first).Should().BeTrue();
        compiled(second).Should().BeTrue();
    }

    #endregion

    // ---------- LazyGetMapReportsAsync ----------

    #region LazyGetMapReportsAsync

    [Fact]
    public async Task LazyGetMapReportsAsync_ShouldReturnMappedMapReports()
    {
        // Arrange
        var location = CreateLocation(LocationType.Building);
        var lazy = new LazyLoadingDto { Skip = 0, Take = 50 };
        IReadOnlyList<Report> reports = new List<Report> { CreateReport(location: location), CreateReport(location: location) };
        LogArrange(nameof(reports), reports);

        SetupFilter(reports);

        // Act
        var result = await _reportService.LazyGetMapReportsAsync(location.Id, lazy);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().OnlyContain(r => r.LocationId == location.Id);
    }

    [Fact]
    public async Task LazyGetMapReportsAsync_ShouldOnlyMatchReportsOfTheRequestedLocation()
    {
        // Arrange
        var building = CreateLocation(LocationType.Building);
        var otherBuilding = CreateLocation(LocationType.Building);
        var inLocation = CreateReport(location: building);
        var elsewhere = CreateReport(location: otherBuilding);
        building.Reports.Add(inLocation);
        otherBuilding.Reports.Add(elsewhere);

        Expression<Func<Report, bool>>? predicate = null;
        SetupFilter(new List<Report>(), p => predicate = p);

        // Act
        await _reportService.LazyGetMapReportsAsync(building.Id, new LazyLoadingDto { Skip = 0, Take = 10 });

        // Assert
        var compiled = predicate!.Compile();
        compiled(inLocation).Should().BeTrue();
        compiled(elsewhere).Should().BeFalse();
    }

    #endregion

    // ---------- GetByIdAsync ----------

    #region GetByIdAsync

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#")]
    [InlineData("xyz-123")]
    [InlineData("ZZZZ")]
    public async Task GetByIdAsync_ShouldReturnNotFoundWithoutQuerying_WhenIdIsNotValidHex(string idOrShortId)
    {
        // Arrange
        LogArrange(nameof(idOrShortId), idOrShortId);

        // Act
        var result = await _reportService.GetByIdAsync(idOrShortId);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
        result.ErrorMessage.Should().Be("Report not found");

        _reportRepositoryMock.Verify(
            r => r.FilterAsync(
                It.IsAny<Expression<Func<Report, bool>>>(), It.IsAny<Expression<Func<Report, object?>>[]?>(),
                It.IsAny<Expression<Func<Report, object?>>?>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNotFoundWithoutQuerying_WhenIdIsLongerThanAGuid()
    {
        // Arrange
        var tooLong = new string('a', 33);
        LogArrange(nameof(tooLong), tooLong);

        // Act
        var result = await _reportService.GetByIdAsync(tooLong);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldMatchTheExactReport_WhenAFullIdWithHashAndDashesIsGiven()
    {
        // Arrange
        var report = CreateReport();
        var other = CreateReport();
        LogArrange(nameof(report), report);

        Expression<Func<Report, bool>>? predicate = null;
        SetupFilter(new List<Report> { report }, p => predicate = p);

        // Act
        var result = await _reportService.GetByIdAsync($"#{report.Id}");
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(report.Id);

        var compiled = predicate!.Compile();
        compiled(report).Should().BeTrue();
        compiled(other).Should().BeFalse();
    }

    [Theory]
    [InlineData(0, HttpStatusCode.NotFound, "Report not found")]
    [InlineData(2, HttpStatusCode.Conflict, "The short number matches more than one report, use the full id")]
    public async Task GetByIdAsync_ShouldReturnFailure_WhenShortIdMatchesNoneOrMoreThanOneReport(int matches, HttpStatusCode expectedStatus, string expectedMessage)
    {
        // Arrange
        IReadOnlyList<Report> reports = Enumerable.Range(0, matches).Select(_ => CreateReport()).ToList();
        LogArrange(nameof(reports), reports);

        SetupFilter(reports);

        // Act
        var result = await _reportService.GetByIdAsync("968FCB28");
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(expectedStatus);
        result.ErrorMessage.Should().Be(expectedMessage);

        // two rows are enough to know the short number is ambiguous
        _reportRepositoryMock.Verify(
            r => r.FilterAsync(
                It.IsAny<Expression<Func<Report, bool>>>(), It.IsAny<Expression<Func<Report, object?>>[]?>(),
                It.IsAny<Expression<Func<Report, object?>>?>(), It.IsAny<bool>(), It.IsAny<int?>(), 2,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnTheReport_WhenShortIdMatchesExactlyOne()
    {
        // Arrange
        var report = CreateReport();
        LogArrange(nameof(report), report);

        SetupFilter(new List<Report> { report });

        // Act
        var result = await _reportService.GetByIdAsync("968FCB28");
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(report.Id);
    }

    #endregion

    // ---------- CreateForLocationIdAsync ----------

    #region CreateForLocationIdAsync

    [Fact]
    public async Task CreateForLocationIdAsync_ShouldReturnNotFound_WhenLocationDoesNotExist()
    {
        // Arrange
        var dto = CreateForLocationDto(Guid.NewGuid());
        LogArrange(nameof(dto), dto);

        _locationRepositoryMock.Setup(r => r.GetByIdAsync(dto.LocationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location?)null);

        // Act
        var result = await _reportService.CreateForLocationIdAsync(Guid.NewGuid(), dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
        result.ErrorMessage.Should().Be("Location not found");

        _reportRepositoryMock.Verify(r => r.AddReport(It.IsAny<Report>()), Times.Never);
    }

    [Fact]
    public async Task CreateForLocationIdAsync_ShouldReturnInternalServerErrorAndNotNotify_WhenNothingWasSaved()
    {
        // Arrange
        var location = CreateLocation();
        var dto = CreateForLocationDto(location.Id);
        LogArrange(nameof(dto), dto);

        _locationRepositoryMock.Setup(r => r.GetByIdAsync(location.Id, It.IsAny<CancellationToken>())).ReturnsAsync(location);
        SetupSave(false);

        // Act
        var result = await _reportService.CreateForLocationIdAsync(Guid.NewGuid(), dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        result.ErrorMessage.Should().Be("no changes where applied to the DB");

        _notificationServiceMock.Verify(
            n => n.CreateAsync(
                It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateForLocationIdAsync_ShouldCreatePendingReportAndNotifyOwner_WhenSaveSucceeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var location = CreateLocation(LocationType.Building, ReportStateEnum.Normal, 2);
        var dto = CreateForLocationDto(location.Id);
        LogArrange(nameof(dto), dto);

        Report? added = null;
        _locationRepositoryMock.Setup(r => r.GetByIdAsync(location.Id, It.IsAny<CancellationToken>())).ReturnsAsync(location);
        _reportRepositoryMock.Setup(r => r.AddReport(It.IsAny<Report>())).Callback<Report>(r => added = r);
        SetupSave(true);
        SetupNotification(Result<NotificationDto>.Success(Fixture.Create<NotificationDto>(), HttpStatusCode.Created));

        // Act
        var result = await _reportService.CreateForLocationIdAsync(userId, dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.Created);

        added.Should().NotBeNull();
        added!.State.Should().Be(ReportStateEnum.Pending);
        added.UserId.Should().Be(userId);
        added.LocationId.Should().Be(location.Id);
        added.Category.Should().Be(dto.Category);
        added.Severity.Should().Be(dto.Severity);
        added.Description.Should().Be(dto.Description.Trim());

        result.Value!.Report.Id.Should().Be(added.Id);
        result.Value.Location.Id.Should().Be(location.Id);
        result.Value.Location.ActiveReportsCount.Should().Be(3);
        result.Value.Location.Status.Should().Be(ReportStateEnum.Pending);

        _notificationServiceMock.Verify(
            n => n.CreateAsync(userId, NotificationType.ReportReceived, It.IsAny<string>(), location.Title, added.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(ReportStateEnum.Normal, ReportStateEnum.Pending)]
    [InlineData(ReportStateEnum.Fixed, ReportStateEnum.Pending)]
    [InlineData(ReportStateEnum.InProgress, ReportStateEnum.Pending)]
    [InlineData(ReportStateEnum.Pending, ReportStateEnum.Pending)]
    [InlineData(ReportStateEnum.NeedsFollowUp, ReportStateEnum.NeedsFollowUp)] // already more urgent than Pending
    public async Task CreateForLocationIdAsync_ShouldRaiseTheLocationStatusToPendingAtLeast(ReportStateEnum current, ReportStateEnum expected)
    {
        // Arrange
        var location = CreateLocation(LocationType.Building, current, 0);
        var dto = CreateForLocationDto(location.Id);
        LogArrange(nameof(current), current);

        _locationRepositoryMock.Setup(r => r.GetByIdAsync(location.Id, It.IsAny<CancellationToken>())).ReturnsAsync(location);
        SetupSave(true);
        SetupNotification(Result<NotificationDto>.Success(Fixture.Create<NotificationDto>(), HttpStatusCode.Created));

        // Act
        var result = await _reportService.CreateForLocationIdAsync(Guid.NewGuid(), dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Location.Status.Should().Be(expected);
        result.Value.Location.ActiveReportsCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateForLocationIdAsync_ShouldStillSucceed_WhenNotificationFails()
    {
        // Arrange
        var location = CreateLocation();
        var dto = CreateForLocationDto(location.Id);
        LogArrange(nameof(dto), dto);

        _locationRepositoryMock.Setup(r => r.GetByIdAsync(location.Id, It.IsAny<CancellationToken>())).ReturnsAsync(location);
        SetupSave(true);
        SetupNotification(Result<NotificationDto>.Failure("boom", HttpStatusCode.InternalServerError));

        // Act
        var result = await _reportService.CreateForLocationIdAsync(Guid.NewGuid(), dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateForLocationIdAsync_ShouldUploadImagesInsteadOfSavingItself_WhenImagesAreSent()
    {
        // Arrange
        var location = CreateLocation();
        var images = new List<IFormFile> { CreateFileMock().Object };
        var dto = CreateForLocationDto(location.Id, images);
        LogArrange(nameof(dto), dto);

        Report? added = null;
        _locationRepositoryMock.Setup(r => r.GetByIdAsync(location.Id, It.IsAny<CancellationToken>())).ReturnsAsync(location);
        _reportRepositoryMock.Setup(r => r.AddReport(It.IsAny<Report>())).Callback<Report>(r => added = r);
        _imageServiceMock
            .Setup(i => i.UploadForReportAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<IFormFile>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<ImageDto>>.Success(new List<ImageDto>(), HttpStatusCode.Created));
        SetupNotification(Result<NotificationDto>.Success(Fixture.Create<NotificationDto>(), HttpStatusCode.Created));

        // Act
        var result = await _reportService.CreateForLocationIdAsync(Guid.NewGuid(), dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // the image service saves the tracked report together with the images
        _imageServiceMock.Verify(
            i => i.UploadForReportAsync(added!.Id, It.IsAny<IReadOnlyList<IFormFile>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _reportRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateForLocationIdAsync_ShouldReturnTheUploadFailureAndNotNotify_WhenUploadingImagesFails()
    {
        // Arrange
        var location = CreateLocation();
        var dto = CreateForLocationDto(location.Id, new List<IFormFile> { CreateFileMock().Object });
        LogArrange(nameof(dto), dto);

        _locationRepositoryMock.Setup(r => r.GetByIdAsync(location.Id, It.IsAny<CancellationToken>())).ReturnsAsync(location);
        _imageServiceMock
            .Setup(i => i.UploadForReportAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<IFormFile>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<ImageDto>>.Failure("Failed to upload the images to the image storage", HttpStatusCode.InternalServerError));

        // Act
        var result = await _reportService.CreateForLocationIdAsync(Guid.NewGuid(), dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        result.ErrorMessage.Should().Be("Failed to upload the images to the image storage");

        _notificationServiceMock.Verify(
            n => n.CreateAsync(
                It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    // ---------- CreateForCoordinates ----------

    #region CreateForCoordinates

    [Fact]
    public async Task CreateForCoordinates_ShouldReturnInternalServerError_WhenNothingWasSaved()
    {
        // Arrange
        var dto = CreateForCoordinatesDto();
        LogArrange(nameof(dto), dto);

        SetupSave(false);

        // Act
        var result = await _reportService.CreateForCoordinates(Guid.NewGuid(), dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task CreateForCoordinates_ShouldCreateAPinLocationAndReport_WhenSaveSucceeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var dto = CreateForCoordinatesDto();
        LogArrange(nameof(dto), dto);

        Location? addedLocation = null;
        Report? addedReport = null;
        _locationRepositoryMock.Setup(r => r.AddLocation(It.IsAny<Location>())).Callback<Location>(l => addedLocation = l);
        _reportRepositoryMock.Setup(r => r.AddReport(It.IsAny<Report>())).Callback<Report>(r => addedReport = r);
        SetupSave(true);
        SetupNotification(Result<NotificationDto>.Success(Fixture.Create<NotificationDto>(), HttpStatusCode.Created));

        // Act
        var result = await _reportService.CreateForCoordinates(userId, dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.Created);

        addedLocation.Should().NotBeNull();
        addedLocation!.Type.Should().Be(LocationType.Pin);
        addedLocation.Coordinates.Lat.Should().Be(dto.Coordinates.Lat);
        addedLocation.Coordinates.Lng.Should().Be(dto.Coordinates.Lng);

        addedReport!.LocationId.Should().Be(addedLocation.Id);
        addedReport.UserId.Should().Be(userId);
        addedReport.State.Should().Be(ReportStateEnum.Pending);

        result.Value!.Location.Type.Should().Be(LocationType.Pin);
        result.Value.Location.Status.Should().Be(ReportStateEnum.Pending);
        result.Value.Location.ActiveReportsCount.Should().Be(1);
        result.Value.Report.LocationId.Should().Be(addedLocation.Id);
    }

    #endregion

    // ---------- UpdateStateAsync ----------

    #region UpdateStateAsync

    [Fact]
    public async Task UpdateStateAsync_ShouldReturnNotFound_WhenReportDoesNotExist()
    {
        // Arrange
        var dto = new ReportUpdateStateDto { Id = Guid.NewGuid(), State = ReportStateEnum.Fixed };
        LogArrange(nameof(dto), dto);

        _reportRepositoryMock.Setup(r => r.UpdateState(dto, It.IsAny<CancellationToken>())).ReturnsAsync((Report?)null);

        // Act
        var result = await _reportService.UpdateStateAsync(dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
        result.ErrorMessage.Should().Be("Report not found");
    }

    [Theory]
    [InlineData(ReportStateEnum.InProgress, NotificationType.ReportInProgress)]
    [InlineData(ReportStateEnum.Fixed, NotificationType.ReportFixed)]
    [InlineData(ReportStateEnum.NeedsFollowUp, NotificationType.ReportNeedsFollowUp)]
    public async Task UpdateStateAsync_ShouldNotifyTheOwner_WhenTheStateChangedToOneTheUserCaresAbout(ReportStateEnum newState, NotificationType expectedType)
    {
        // Arrange
        var location = CreateLocation();
        var report = CreateReport(newState, location: location);
        var dto = new ReportUpdateStateDto { Id = report.Id, State = newState };
        LogArrange(nameof(dto), dto);

        _reportRepositoryMock.Setup(r => r.UpdateState(dto, It.IsAny<CancellationToken>())).ReturnsAsync(report);
        _locationRepositoryMock.Setup(r => r.GetByIdAsync(report.LocationId, It.IsAny<CancellationToken>())).ReturnsAsync(location);
        SetupSave(true);
        SetupNotification(Result<NotificationDto>.Success(Fixture.Create<NotificationDto>(), HttpStatusCode.Created));

        // Act
        var result = await _reportService.UpdateStateAsync(dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.State.Should().Be(newState);

        _notificationServiceMock.Verify(
            n => n.CreateAsync(report.UserId, expectedType, It.IsAny<string>(), location.Title, report.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(ReportStateEnum.Normal)]
    [InlineData(ReportStateEnum.Pending)]
    public async Task UpdateStateAsync_ShouldNotNotify_WhenTheNewStateIsNotWorthANotification(ReportStateEnum newState)
    {
        // Arrange
        var report = CreateReport(newState);
        var dto = new ReportUpdateStateDto { Id = report.Id, State = newState };
        LogArrange(nameof(dto), dto);

        _reportRepositoryMock.Setup(r => r.UpdateState(dto, It.IsAny<CancellationToken>())).ReturnsAsync(report);
        SetupSave(true);

        // Act
        var result = await _reportService.UpdateStateAsync(dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _locationRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _notificationServiceMock.Verify(
            n => n.CreateAsync(
                It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateStateAsync_ShouldSucceedWithoutNotifying_WhenTheReportAlreadyHadThatState()
    {
        // Arrange
        var report = CreateReport(ReportStateEnum.Fixed);
        var dto = new ReportUpdateStateDto { Id = report.Id, State = ReportStateEnum.Fixed };
        LogArrange(nameof(dto), dto);

        _reportRepositoryMock.Setup(r => r.UpdateState(dto, It.IsAny<CancellationToken>())).ReturnsAsync(report);
        SetupSave(false); // nothing changed in the DB

        // Act
        var result = await _reportService.UpdateStateAsync(dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _notificationServiceMock.Verify(
            n => n.CreateAsync(
                It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdateStateAsync_ShouldStillSucceed_WhenNotificationFails(bool throwsException)
    {
        // Arrange
        var report = CreateReport(ReportStateEnum.Fixed);
        var dto = new ReportUpdateStateDto { Id = report.Id, State = ReportStateEnum.Fixed };
        LogArrange(nameof(dto), dto);

        _reportRepositoryMock.Setup(r => r.UpdateState(dto, It.IsAny<CancellationToken>())).ReturnsAsync(report);
        _locationRepositoryMock.Setup(r => r.GetByIdAsync(report.LocationId, It.IsAny<CancellationToken>())).ReturnsAsync((Location?)null);
        SetupSave(true);
        if (throwsException)
            _notificationServiceMock.Setup(n => n.CreateAsync(It.IsAny<Guid>(), It.IsAny<NotificationType>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("notification unavailable"));
        else
            SetupNotification(Result<NotificationDto>.Failure("boom", HttpStatusCode.InternalServerError));

        // Act
        var result = await _reportService.UpdateStateAsync(dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.State.Should().Be(ReportStateEnum.Fixed);
    }

    #endregion

    // ---------- RateAsync ----------

    #region RateAsync

    [Fact]
    public async Task RateAsync_ShouldReturnNotFound_WhenReportDoesNotExist()
    {
        // Arrange
        var dto = new ReportRateDto { Id = Guid.NewGuid(), Rate = 4 };
        LogArrange(nameof(dto), dto);

        _reportRepositoryMock.Setup(r => r.GetTrackedByIdAsync(dto.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Report?)null);

        // Act
        var result = await _reportService.RateAsync(Guid.NewGuid(), dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RateAsync_ShouldReturnForbidden_WhenReportBelongsToAnotherUser()
    {
        // Arrange
        var report = CreateReport(ReportStateEnum.Fixed, userId: Guid.NewGuid());
        var dto = new ReportRateDto { Id = report.Id, Rate = 4 };
        LogArrange(nameof(dto), dto);

        _reportRepositoryMock.Setup(r => r.GetTrackedByIdAsync(dto.Id, It.IsAny<CancellationToken>())).ReturnsAsync(report);

        // Act
        var result = await _reportService.RateAsync(Guid.NewGuid(), dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        result.ErrorMessage.Should().Be("You can only access your own reports");
        report.Rate.Should().BeNull();
    }

    [Fact]
    public async Task RateAsync_ShouldReturnBadRequest_WhenRateIsTheSameAsTheCurrentOne()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var report = CreateReport(ReportStateEnum.Fixed, userId);
        report.TryRate(4);
        var dto = new ReportRateDto { Id = report.Id, Rate = 4 };
        LogArrange(nameof(dto), dto);

        _reportRepositoryMock.Setup(r => r.GetTrackedByIdAsync(dto.Id, It.IsAny<CancellationToken>())).ReturnsAsync(report);

        // Act
        var result = await _reportService.RateAsync(userId, dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.ErrorMessage.Should().Be("Rate can not remain the same");
    }

    [Theory]
    [InlineData(ReportStateEnum.Pending)]
    [InlineData(ReportStateEnum.InProgress)]
    [InlineData(ReportStateEnum.NeedsFollowUp)]
    public async Task RateAsync_ShouldReturnBadRequest_WhenReportIsNotFixed(ReportStateEnum state)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var report = CreateReport(state, userId);
        var dto = new ReportRateDto { Id = report.Id, Rate = 3 };
        LogArrange(nameof(dto), dto);

        _reportRepositoryMock.Setup(r => r.GetTrackedByIdAsync(dto.Id, It.IsAny<CancellationToken>())).ReturnsAsync(report);

        // Act
        var result = await _reportService.RateAsync(userId, dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.ErrorMessage.Should().Be("A report can only be rated after it is fixed");
        _reportRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task RateAsync_ShouldReject_WhenRateIsOutOfRange(int rate)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var report = CreateReport(ReportStateEnum.Fixed, userId);
        var dto = new ReportRateDto { Id = report.Id, Rate = rate };
        LogArrange(nameof(dto), dto);

        _reportRepositoryMock.Setup(r => r.GetTrackedByIdAsync(dto.Id, It.IsAny<CancellationToken>())).ReturnsAsync(report);

        // Act
        var result = await _reportService.RateAsync(userId, dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        report.Rate.Should().BeNull();
        _reportRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RateAsync_ShouldReturnInternalServerError_WhenNothingWasSaved()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var report = CreateReport(ReportStateEnum.Fixed, userId);
        var dto = new ReportRateDto { Id = report.Id, Rate = 5 };
        LogArrange(nameof(dto), dto);

        _reportRepositoryMock.Setup(r => r.GetTrackedByIdAsync(dto.Id, It.IsAny<CancellationToken>())).ReturnsAsync(report);
        SetupSave(false);

        // Act
        var result = await _reportService.RateAsync(userId, dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task RateAsync_ShouldSaveTheRateAndReturnIt_WhenReportIsFixedAndOwnedByTheUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var report = CreateReport(ReportStateEnum.Fixed, userId);
        var dto = new ReportRateDto { Id = report.Id, Rate = 5 };
        LogArrange(nameof(dto), dto);

        _reportRepositoryMock.Setup(r => r.GetTrackedByIdAsync(dto.Id, It.IsAny<CancellationToken>())).ReturnsAsync(report);
        SetupSave(true);

        // Act
        var result = await _reportService.RateAsync(userId, dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Rate.Should().Be(5);
        report.Rate.Should().Be(5);
        _reportRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    // ---------- DeleteAsync ----------

    #region DeleteAsync

    [Fact]
    public async Task DeleteAsync_ShouldReturnNotFound_WhenReportDoesNotExist()
    {
        // Arrange
        var id = Guid.NewGuid();
        LogArrange(nameof(id), id);

        _reportRepositoryMock.Setup(r => r.RemoveById(id, It.IsAny<CancellationToken>())).ReturnsAsync((Report?)null);

        // Act
        var result = await _reportService.DeleteAsync(id);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnInternalServerError_WhenNothingWasSaved()
    {
        // Arrange
        var report = CreateReport();
        LogArrange(nameof(report), report);

        _reportRepositoryMock.Setup(r => r.RemoveById(report.Id, It.IsAny<CancellationToken>())).ReturnsAsync(report);
        SetupSave(false);

        // Act
        var result = await _reportService.DeleteAsync(report.Id);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnNoContent_WhenReportIsDeleted()
    {
        // Arrange
        var report = CreateReport();
        LogArrange(nameof(report), report);

        _reportRepositoryMock.Setup(r => r.RemoveById(report.Id, It.IsAny<CancellationToken>())).ReturnsAsync(report);
        SetupSave(true);

        // Act
        var result = await _reportService.DeleteAsync(report.Id);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    #endregion

    // ---------- stats ----------

    #region Stats

    [Fact]
    public async Task GetMaintenanceStatsAsync_ShouldReturnTheStatsFromTheRepository()
    {
        // Arrange
        var stats = Fixture.Create<MaintenanceStatsDto>();
        LogArrange(nameof(stats), stats);

        _reportRepositoryMock.Setup(r => r.GetMaintenanceStatsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(stats);

        // Act
        var result = await _reportService.GetMaintenanceStatsAsync();
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(stats);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetHistoryStatsAsync_ShouldReturnTheStatsOfTheGivenUserOrOfEveryone(bool forOneUser)
    {
        // Arrange
        Guid? userId = forOneUser ? Guid.NewGuid() : null;
        var stats = Fixture.Create<ReportHistoryStatsDto>();
        LogArrange(nameof(userId), userId);
        LogArrange(nameof(stats), stats);

        _reportRepositoryMock.Setup(r => r.GetHistoryStatsAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(stats);

        // Act
        var result = await _reportService.GetHistoryStatsAsync(userId);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(stats);
    }

    #endregion
}