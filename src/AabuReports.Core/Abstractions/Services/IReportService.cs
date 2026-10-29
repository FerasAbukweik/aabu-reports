using AabuReports.Core.Dtos.LazyLoading;
using AabuReports.Core.Results;
using AabuReports.Core.Dtos.Reports;

namespace AabuReports.Core.Abstractions.Services;

public interface IReportService
{
    /// <summary>
    /// gets one page of the reports, newest first, filtered by the filterDto.
    /// Used by the reports table (the user's own reports) and by the admin (everyone's reports).
    /// </summary>
    /// <param name="userId"> only the reports of this user, null for the reports of everyone </param>
    Task<Result<IReadOnlyList<ReportDto>>> LazyGetAllAsync(Guid? userId, ReportFilterDto filterDto, LazyLoadingDto lazyLoadingDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// gets a report by its id, only its owner or an admin can see it
    /// </summary>
    /// <returns> result object with the report, 404 if it doesnt exist, 403 if it is not the user's report </returns>
    Task<Result<ReportDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// creates a new report with the Pending state for already existing location
    /// </summary>
    /// <returns> result object with the created report, or 404 if the location doesnt exist </returns>
    Task<Result<ReportCreateResponseDto>> CreateForLocationIdAsync(Guid userId, ReportCreateForLocationDto reportData, CancellationToken cancellationToken = default);

    /// <summary>
    /// creates a new report with the Pending state for a new location
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="reportData"></param>
    /// <param name="cancellationToken"></param>
    /// <returns> result object with the created report </returns>
    Task<Result<ReportCreateResponseDto>> CreateForCoordinates(Guid userId, ReportCreateForCoordinatesDto reportData,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// changes the state of a report (the rate is cleared if the new state is not Fixed)
    /// </summary>
    /// <returns> result object with the updated report, or 404 if it doesnt exist </returns>
    Task<Result<ReportDto>> UpdateStateAsync(ReportUpdateStateDto reportUpdateStateDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// the owner of the report rates the fix
    /// </summary>
    /// <returns> result object with the rated report, 404 if it doesnt exist, 403 if it is not the user's report, 400 if the report is not fixed yet </returns>
    Task<Result<ReportDto>> RateAsync(Guid userId, ReportRateDto reportRateDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// deletes a report
    /// </summary>
    /// <returns> result object, 404 if it doesnt exist </returns>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// gets one page of the reports of a location, in the short shape the map needs
    /// </summary>
    /// <param name="locationId"></param>
    /// <param name="lazyLoadingDto"></param>
    /// <param name="cancellationToken"></param>
    /// <returns> a list of the reports of the location </returns>
    Task<Result<IReadOnlyList<ReportMapDto>>> LazyGetMapReportsAsync(Guid locationId, LazyLoadingDto lazyLoadingDto, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// gets the numbers for the maintenance stats cards
    /// </summary>
    /// <returns> result object with the stats </returns>
    Task<Result<MaintenanceStatsDto>> GetMaintenanceStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// gets the numbers for the stats cards of the reports table (the reports of one user, or everyone's if userId is null)
    /// </summary>
    /// <returns> result object with the stats </returns>
    Task<Result<ReportHistoryStatsDto>> GetHistoryStatsAsync(Guid? userId, CancellationToken cancellationToken = default);
}
