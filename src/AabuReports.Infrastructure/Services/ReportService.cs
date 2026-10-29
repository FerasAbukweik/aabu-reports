using System.Net;
using AabuReports.Core.Abstractions.Repositories;
using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.Images;
using AabuReports.Core.Dtos.LazyLoading;
using AabuReports.Core.Dtos.Reports;
using AabuReports.Core.Entities;
using AabuReports.Core.Enums;
using AabuReports.Core.Extensions;
using AabuReports.Core.Results;
using Microsoft.Extensions.Logging;

namespace AabuReports.Infrastructure.Services;

public class ReportService(
    IReportRepository reportRepository,
    ILocationRepository locationRepository,
    IImageService imageService,
    INotificationService notificationService,
    ILogger<ReportService> logger) : IReportService
{
    private const string NotFoundMessage = "Report not found";
    private const string LocationNotFoundMessage = "Location not found";
    private const string NotYourReportMessage = "You can only access your own reports";
    private const string NotFixedMessage = "A report can only be rated after it is fixed";

    // the text of the notifications sent to the owner of the report (the location title goes in the notification's Subject)
    private const string ReceivedNotificationMessage = "تم استلام بلاغك وسيتم مراجعته قريبًا";
    private const string InProgressNotificationMessage = "بدأ العمل على بلاغك";
    private const string FixedNotificationMessage = "تم إصلاح المشكلة، يمكنك الآن تقييم الإصلاح";
    private const string NeedsFollowUpNotificationMessage = "يحتاج بلاغك إلى متابعة إضافية";
    
    public async Task<Result<IReadOnlyList<ReportDto>>> LazyGetAllAsync(Guid? userId, ReportFilterDto filterDto, LazyLoadingDto lazyLoadingDto, CancellationToken cancellationToken = default)
    {
        var reports = await reportRepository.FilterAsync(
            filterDto.ToPredicate().And(r => (userId == null || r.UserId == userId)),
            [r => r.Images, r => r.Location],
            r => r.ReportDate,
            true,
            lazyLoadingDto.Skip,
            lazyLoadingDto.Take,
            cancellationToken);

        return Result<IReadOnlyList<ReportDto>>.Success(reports.Select(r => r.ToDto()).ToList());
    }
    
    public async Task<Result<IReadOnlyList<ReportMapDto>>> LazyGetMapReportsAsync(Guid locationId, LazyLoadingDto lazyLoadingDto, CancellationToken cancellationToken = default)
    {
        var lastWeekDate = DateTime.UtcNow.AddDays(-7);
        
        var filtered = await reportRepository.FilterAsync(
            r => (
                r.LocationId == locationId && (
                    r.Location!.Reports.Any(r => (int)r.State > (int)ReportStateEnum.Fixed) ||
                    r.Location!.Reports.Any(r => r.FixDate >= lastWeekDate)
                )
            ),
            [],
            r => r.ReportDate,
            true,
            lazyLoadingDto.Skip,
            lazyLoadingDto.Take,
            cancellationToken);

        return Result<IReadOnlyList<ReportMapDto>>.Success(filtered.Select(r => r.ToMapDto()).ToList());
    }

    public async Task<Result<ReportDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var report = await reportRepository.GetByIdAsync(id, cancellationToken);
        if (report is null)
            return Result<ReportDto>.Failure(NotFoundMessage, HttpStatusCode.NotFound);

        return Result<ReportDto>.Success(report.ToDto());
    }
    public async Task<Result<ReportCreateResponseDto>> CreateForLocationIdAsync(Guid userId, ReportCreateForLocationDto reportData, CancellationToken cancellationToken = default)
    {
        var location = await locationRepository.GetByIdAsync(reportData.LocationId, cancellationToken);
        if (location is null)
            return Result<ReportCreateResponseDto>.Failure(LocationNotFoundMessage, HttpStatusCode.NotFound);

        return await CreateReportAsync(userId, location, reportData, cancellationToken);
    }
    public async Task<Result<ReportCreateResponseDto>> CreateForCoordinates(Guid userId, ReportCreateForCoordinatesDto reportData, CancellationToken cancellationToken = default)
    {
        var newLocation = new Location()
        {
            Coordinates = reportData.Coordinates,
            Title = "موقع مخصص",
            Type = LocationType.Pin,
            State = ReportStateEnum.Pending,
            ReportsCount = 1
        };
        locationRepository.AddLocation(newLocation);

        return await CreateReportAsync(userId, newLocation, reportData, cancellationToken);
    }
    public async Task<Result<ReportDto>> UpdateStateAsync(ReportUpdateStateDto reportUpdateStateDto, CancellationToken cancellationToken = default)
    {
        var updated = await reportRepository.UpdateState(reportUpdateStateDto, cancellationToken);
        if (updated is null)
            return Result<ReportDto>.Failure(NotFoundMessage, HttpStatusCode.NotFound);

        // saving returns false when the report already had this state, that is not an error (and nobody is notified)
        if (await reportRepository.SaveChangesAsync(cancellationToken))
            await NotifyStateChangedAsync(updated, cancellationToken);

        return Result<ReportDto>.Success(updated.ToDto());
    }
    public async Task<Result<ReportDto>> RateAsync(Guid userId, ReportRateDto reportRateDto, CancellationToken cancellationToken = default)
    {
        var report = await reportRepository.GetTrackedByIdAsync(reportRateDto.Id, cancellationToken);
        if (report is null)
            return Result<ReportDto>.Failure(NotFoundMessage, HttpStatusCode.NotFound);

        if (report.UserId != userId)
            return Result<ReportDto>.Failure(NotYourReportMessage, HttpStatusCode.Forbidden);
        
        if(report.Rate == reportRateDto.Rate)
            return Result<ReportDto>.Failure("Rate can not remain the same");
            
        if (!report.TryRate(reportRateDto.Rate))
            return Result<ReportDto>.Failure(NotFixedMessage);

        if (!await reportRepository.SaveChangesAsync(cancellationToken))
        {
            var errorMessage = "no changes where applied to the DB";
            logger.LogError("{ServiceName}.{MethodName} " + errorMessage,
                nameof(ReportService), nameof(RateAsync));

            return Result<ReportDto>.Failure(errorMessage, HttpStatusCode.InternalServerError);
        }
        return Result<ReportDto>.Success(report.ToDto());
    }
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var removed = await reportRepository.RemoveById(id, cancellationToken);
        if (removed is null)
            return Result.Failure(NotFoundMessage, HttpStatusCode.NotFound);

        if (!await reportRepository.SaveChangesAsync(cancellationToken))
        {
            var errorMessage = "no changes where applied to the DB";
            logger.LogError("{ServiceName}.{MethodName} " + errorMessage,
                nameof(ReportService), nameof(DeleteAsync));

            return Result.Failure(errorMessage, HttpStatusCode.InternalServerError);
        }

        return Result.Success(HttpStatusCode.NoContent);
    }

    public async Task<Result<MaintenanceStatsDto>> GetMaintenanceStatsAsync(CancellationToken cancellationToken = default)
    {
        var stats = await reportRepository.GetMaintenanceStatsAsync(cancellationToken);

        return Result<MaintenanceStatsDto>.Success(stats);
    }

    public async Task<Result<ReportHistoryStatsDto>> GetHistoryStatsAsync(Guid? userId, CancellationToken cancellationToken = default)
    {
        var stats = await reportRepository.GetHistoryStatsAsync(userId, cancellationToken);

        return Result<ReportHistoryStatsDto>.Success(stats);
    }


    // private
    private async Task<Result<ReportCreateResponseDto>> CreateReportAsync(Guid userId, Location location, ReportCreateDto newReportData, CancellationToken cancellationToken  = default)
    {
        var newReport = new Report
        {
            ReportDate = DateTime.UtcNow,
            Category = newReportData.Category,
            State = ReportStateEnum.Pending,
            Severity = newReportData.Severity,
            Description = newReportData.Description.Trim(),
            LocationId = location.Id,
            UserId = userId
        };

        reportRepository.AddReport(newReport);

        IReadOnlyList<ImageDto> images = [];

        if (newReportData.Images.Count > 0)
        {
            // the report (and its new location, if any) is only in the memory at this point.
            // the image service checks the files first, uploads to Cloudinary as its very last step,
            // then saves the report, the location and the images together with one SaveChanges,
            // so nothing is uploaded if something fails before it, and the upload is undone if the save fails
            var imagesResult = await imageService.UploadForReportAsync(newReport.Id, newReportData.Images, cancellationToken);
            if (!imagesResult.IsSuccess)
                return imagesResult.MapFailure<ReportCreateResponseDto>();

            images = imagesResult.Value!;
        }
        else if (!await reportRepository.SaveChangesAsync(cancellationToken))
        {
            var errorMessage = "no changes where applied to the DB";
            logger.LogError("{ServiceName}.{MethodName} " + errorMessage,
                nameof(ReportService), nameof(CreateReportAsync));

            return Result<ReportCreateResponseDto>.Failure(errorMessage, HttpStatusCode.InternalServerError);
        }

        await NotifyOwnerAsync(userId, NotificationType.ReportReceived, ReceivedNotificationMessage, location.Title, newReport.Id, cancellationToken);

        return Result<ReportCreateResponseDto>.Success(new ReportCreateResponseDto()
        {
            Location = location.ToDto(),
            Report = newReport.ToMapDto(),
        }, HttpStatusCode.Created);
    }

    /// <summary> tells the owner of the report that its state changed (only the states a user cares about) </summary>
    private async Task NotifyStateChangedAsync(Report report, CancellationToken cancellationToken)
    {
        NotificationType type;
        string message;

        switch (report.State)
        {
            case ReportStateEnum.InProgress:
                type = NotificationType.ReportInProgress;
                message = InProgressNotificationMessage;
                break;
            case ReportStateEnum.Fixed:
                type = NotificationType.ReportFixed;
                message = FixedNotificationMessage;
                break;
            case ReportStateEnum.NeedsFollowUp:
                type = NotificationType.ReportNeedsFollowUp;
                message = NeedsFollowUpNotificationMessage;
                break;
            default:
                // Normal and Pending are not worth a notification
                return;
        }

        var location = await locationRepository.GetByIdAsync(report.LocationId, cancellationToken);

        await NotifyOwnerAsync(report.UserId, type, message, location?.Title, report.Id, cancellationToken);
    }

    /// <summary> a notification that fails must not fail the report action that already succeeded, so it is only logged </summary>
    private async Task NotifyOwnerAsync(Guid userId, NotificationType type, string message, string? subject, Guid reportId, CancellationToken cancellationToken)
    {
        var result = await notificationService.CreateAsync(userId, type, message, subject, reportId, cancellationToken);
        if (!result.IsSuccess)
            logger.LogWarning("{ServiceName} could not create a {NotificationType} notification for report {ReportId}: {Error}",
                nameof(ReportService), type, reportId, result.ErrorMessage);
    }
}
