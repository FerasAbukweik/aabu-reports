using aabu_reports.core.Common;
using aabu_reports.core.DTOs.Lazy;
using aabu_reports.core.DTOs.Report;

namespace aabu_reports.core.Interfaces.ServiceContracts;

public interface IReportService
{
    /// <summary>
    /// gets all reports (for admins)
    /// </summary>
    Task<Result<IReadOnlyList<ReportDTO>>> LazyGetAllAsync(LazyDTO lazyDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// gets the reports sent by one user
    /// </summary>
    Task<Result<IReadOnlyList<ReportDTO>>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// gets a report by its id, only its owner or an admin can see it
    /// </summary>
    /// <returns> result object with the report, 404 if it doesnt exist, 403 if it is not the user's report </returns>
    Task<Result<ReportDTO>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// creates a new report with the Pending state for already existing location
    /// </summary>
    /// <returns> result object with the created report, or 404 if the location doesnt exist </returns>
    Task<Result<ReportCreateResponseDTO>> CreateForLocationIdAsync(Guid userId, ReportCreateForLocationDTO reportData, CancellationToken cancellationToken = default);

    /// <summary>
    /// creates a new report with the Pending state for a new location
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="reportData"></param>
    /// <param name="cancellationToken"></param>
    /// <returns> result object with the created report </returns>
    Task<Result<ReportCreateResponseDTO>> CreateForCoordinates(Guid userId, ReportCreateForCoordinatesDTO reportData,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// changes the state of a report (the rate is cleared if the new state is not Fixed)
    /// </summary>
    /// <returns> result object with the updated report, or 404 if it doesnt exist </returns>
    Task<Result<ReportDTO>> UpdateStateAsync(ReportUpdateStateDTO reportUpdateStateDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// the owner of the report rates the fix
    /// </summary>
    /// <returns> result object with the rated report, 404 if it doesnt exist, 403 if it is not the user's report, 400 if the report is not fixed yet </returns>
    Task<Result<ReportDTO>> RateAsync(Guid userId, ReportRateDTO reportRateDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// deletes a report
    /// </summary>
    /// <returns> result object, 404 if it doesnt exist </returns>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<Result<IReadOnlyList<ReportDTO>>> LazyGetLocationReportsAsync(Guid locationId,LazyDTO lazyDto, CancellationToken cancellationToken = default);
}
