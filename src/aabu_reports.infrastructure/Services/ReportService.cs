using System.Net;
using aabu_reports.core.Common;
using aabu_reports.core.DTOs.Lazy;
using aabu_reports.core.DTOs.Report;
using aabu_reports.core.Entities;
using aabu_reports.core.Enums;
using aabu_reports.core.Interfaces.RepositoryContracts;
using aabu_reports.core.Interfaces.ServiceContracts;
using Microsoft.Extensions.Logging;

namespace aabu_reports.infrastructure.Services;

public class ReportService(
    IReportRepository reportRepository,
    ILocationRepository locationRepository,
    ILogger<ReportService> logger) : IReportService
{
    private const string NotFoundMessage = "Report not found";
    private const string LocationNotFoundMessage = "Location not found";
    private const string NotYourReportMessage = "You can only access your own reports";
    private const string NotFixedMessage = "A report can only be rated after it is fixed";

    public async Task<Result<IReadOnlyList<ReportDTO>>> LazyGetAllAsync(LazyDTO lazyDto, CancellationToken cancellationToken = default)
    {
        var reports = await reportRepository.LazyGetAllAsync(lazyDto, cancellationToken);

        return Result<IReadOnlyList<ReportDTO>>.Success(reports.Select(r => r.ToDTO()).ToList());
    }
    public async Task<Result<IReadOnlyList<ReportDTO>>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var reports = await reportRepository.GetByUserIdAsync(userId, cancellationToken);

        return Result<IReadOnlyList<ReportDTO>>.Success(reports.Select(r => r.ToDTO()).ToList());
    }
    public async Task<Result<ReportDTO>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var report = await reportRepository.GetByIdAsync(id, cancellationToken);
        if (report is null)
            return Result<ReportDTO>.Failure(NotFoundMessage, HttpStatusCode.NotFound);

        return Result<ReportDTO>.Success(report.ToDTO());
    }
    public async Task<Result<ReportCreateResponseDTO>> CreateForLocationIdAsync(Guid userId, ReportCreateForLocationDTO reportData, CancellationToken cancellationToken = default)
    {
        var location = await locationRepository.GetByIdAsync(reportData.LocationId, cancellationToken);
        if (location is null)
            return Result<ReportCreateResponseDTO>.Failure(LocationNotFoundMessage, HttpStatusCode.NotFound);

        return await CreateReportAsync(userId, location, reportData, cancellationToken);
    }
    public async Task<Result<ReportCreateResponseDTO>> CreateForCoordinates(Guid userId, ReportCreateForCoordinatesDTO reportData, CancellationToken cancellationToken = default)
    {
        var newLocation = new Location()
        {
            Coordinates = reportData.Coordinates,
            Title = "موقع مخصص",
            Type = LocationTypeEnum.Pin,
            State = ReportState.Pending,
            ReportsCount = 1
        };
        locationRepository.AddLocation(newLocation);

        return await CreateReportAsync(userId, newLocation, reportData, cancellationToken);
    }
    public async Task<Result<ReportDTO>> UpdateStateAsync(ReportUpdateStateDTO reportUpdateStateDto, CancellationToken cancellationToken = default)
    {
        var updated = await reportRepository.UpdateState(reportUpdateStateDto, cancellationToken);
        if (updated is null)
            return Result<ReportDTO>.Failure(NotFoundMessage, HttpStatusCode.NotFound);

        // saving returns false when the report already had this state, that is not an error
        await reportRepository.SaveChangesAsync(cancellationToken);

        return Result<ReportDTO>.Success(updated.ToDTO());
    }
    public async Task<Result<ReportDTO>> RateAsync(Guid userId, ReportRateDTO reportRateDto, CancellationToken cancellationToken = default)
    {
        var report = await reportRepository.GetTrackedByIdAsync(reportRateDto.Id, cancellationToken);
        if (report is null)
            return Result<ReportDTO>.Failure(NotFoundMessage, HttpStatusCode.NotFound);

        if (report.UserId != userId)
            return Result<ReportDTO>.Failure(NotYourReportMessage, HttpStatusCode.Forbidden);
        
        if(report.Rate == reportRateDto.Rate)
            return Result<ReportDTO>.Failure("Rate can not remain the same");
            
        if (!report.TryRate(reportRateDto.Rate))
            return Result<ReportDTO>.Failure(NotFixedMessage);

        if (!await reportRepository.SaveChangesAsync(cancellationToken))
        {
            var errorMessage = "no changes where applied to the DB";
            logger.LogError("{ServiceName}.{MethodName} " + errorMessage,
                nameof(ReportService), nameof(RateAsync));

            return Result<ReportDTO>.Failure(errorMessage, HttpStatusCode.InternalServerError);
        }
        return Result<ReportDTO>.Success(report.ToDTO());
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
    public async Task<Result<IReadOnlyList<ReportDTO>>> LazyGetLocationReportsAsync(Guid locationId,LazyDTO lazyDto, CancellationToken cancellationToken = default)
    {
        var filtered = await reportRepository.FilterAsync(
            r => r.LocationId == locationId,
            [],
            r => r.ReportDate,
            true,
            lazyDto.Skip,
            lazyDto.Take,
            cancellationToken);

        return Result<IReadOnlyList<ReportDTO>>.Success(filtered.Select(r => r.ToDTO()).ToList());
    }
    
    // private
    private async Task<Result<ReportCreateResponseDTO>> CreateReportAsync(Guid userId, Location location, ReportCreate newReportData, CancellationToken cancellationToken  = default)
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

            return Result<ReportCreateResponseDTO>.Failure(errorMessage, HttpStatusCode.InternalServerError);
        }

        return Result<ReportCreateResponseDTO>.Success(new ReportCreateResponseDTO()
        {
            Location = location.ToDTO(),
            Report = newReport.ToDTO()
        }, HttpStatusCode.Created);
    }
}
