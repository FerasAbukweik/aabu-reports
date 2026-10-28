using System.Net;
using AabuReports.Core.Abstractions.Repositories;
using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.LazyLoading;
using AabuReports.Core.Dtos.Reports;
using AabuReports.Core.Entities;
using AabuReports.Core.Enums;
using AabuReports.Core.Results;
using Microsoft.Extensions.Logging;

namespace AabuReports.Infrastructure.Services;

public class ReportService(
    IReportRepository reportRepository,
    ILocationRepository locationRepository,
    ILogger<ReportService> logger) : IReportService
{
    private const string NotFoundMessage = "Report not found";
    private const string LocationNotFoundMessage = "Location not found";
    private const string NotYourReportMessage = "You can only access your own reports";
    private const string NotFixedMessage = "A report can only be rated after it is fixed";

    public async Task<Result<IReadOnlyList<ReportDto>>> LazyGetAllAsync(LazyLoadingDto lazyLoadingDto, CancellationToken cancellationToken = default)
    {
        var reports = await reportRepository.LazyGetAllAsync(lazyLoadingDto, cancellationToken);

        return Result<IReadOnlyList<ReportDto>>.Success(reports.Select(r => r.ToDto()).ToList());
    }
    public async Task<Result<IReadOnlyList<ReportDto>>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var reports = await reportRepository.GetByUserIdAsync(userId, cancellationToken);

        return Result<IReadOnlyList<ReportDto>>.Success(reports.Select(r => r.ToDto()).ToList());
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
            State = ReportState.Pending,
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

        // saving returns false when the report already had this state, that is not an error
        await reportRepository.SaveChangesAsync(cancellationToken);

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
    public async Task<Result<IReadOnlyList<ReportDto>>> LazyGetLocationReportsAsync(Guid locationId,LazyLoadingDto lazyLoadingDto, CancellationToken cancellationToken = default)
    {
        var filtered = await reportRepository.FilterAsync(
            r => r.LocationId == locationId,
            [],
            r => r.ReportDate,
            true,
            lazyLoadingDto.Skip,
            lazyLoadingDto.Take,
            cancellationToken);

        return Result<IReadOnlyList<ReportDto>>.Success(filtered.Select(r => r.ToDto()).ToList());
    }
    
    // private
    private async Task<Result<ReportCreateResponseDto>> CreateReportAsync(Guid userId, Location location, ReportCreateDto newReportData, CancellationToken cancellationToken  = default)
    {
        var newReport = new Report
        {
            ReportDate = DateTime.UtcNow,
            Category = newReportData.Category,
            State = ReportState.Pending,
            Severity = newReportData.Severity,
            Description = newReportData.Description.Trim(),
            LocationId = location.Id,
            UserId = userId
        };

        reportRepository.AddReport(newReport);

        if (!await reportRepository.SaveChangesAsync(cancellationToken))
        {
            var errorMessage = "no changes where applied to the DB";
            logger.LogError("{ServiceName}.{MethodName} " + errorMessage,
                nameof(ReportService), nameof(CreateForLocationIdAsync));

            return Result<ReportCreateResponseDto>.Failure(errorMessage, HttpStatusCode.InternalServerError);
        }

        return Result<ReportCreateResponseDto>.Success(new ReportCreateResponseDto()
        {
            Location = location.ToDto(),
            Report = newReport.ToDto()
        }, HttpStatusCode.Created);
    }
}
