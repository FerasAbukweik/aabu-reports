using System.Linq.Expressions;
using AabuReports.Core.Dtos.LazyLoading;
using AabuReports.Core.Dtos.Reports;
using AabuReports.Core.Entities;

namespace AabuReports.Core.Abstractions.Repositories;

public interface IReportRepository
{
    /// <summary>
    /// add the report to the memory (doesnt save it to DB)
    /// </summary>
    /// <param name="report"> report to be added </param>
    void AddReport(Report report);

    /// <summary>
    /// remove the report in the memory (doesnt delete it from the DB until changes are saved)
    /// </summary>
    /// <param name="id">id for the report to be removed</param>
    /// <param name="cancellationToken"></param>
    /// <returns> returns the removed report -- returns null if no report was found</returns>
    Task<Report?> RemoveById(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// finds a report by its id (entity is not tracked)
    /// </summary>
    /// <returns> the report, or null if it doesnt exist </returns>
    Task<Report?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// finds a report by its id (entity is tracked, so changes on it are saved by SaveChangesAsync)
    /// </summary>
    /// <returns> the report, or null if it doesnt exist </returns>
    Task<Report?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// filter based on input
    /// </summary>
    /// <param name="predicate"></param>
    /// <param name="include"></param>
    /// <param name="orderBy"></param>
    /// <param name="orderByDesc"></param>
    /// <param name="skip"></param>
    /// <param name="take"></param>
    /// <param name="cancellationToken"></param>
    /// <returns> filtered IReadOnlyList </returns>
    Task<IReadOnlyList<Report>> FilterAsync(
        Expression<Func<Report, bool>> predicate,
        Expression<Func<Report, object?>>[]? include = null,
        Expression<Func<Report, object?>>? orderBy = null,
        bool orderByDesc = false,
        int? skip = null,
        int? take = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// loads all reports, newest first (entities are not tracked)
    /// </summary>
    Task<IReadOnlyList<Report>> LazyGetAllAsync(LazyLoadingDto lazyLoadingDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// loads the reports of one user, newest first (entities are not tracked)
    /// </summary>
    Task<IReadOnlyList<Report>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// changes the state of a report in the memory (doesnt save it to DB), the rate is cleared if it is no longer fixed
    /// </summary>
    /// <param name="reportUpdateStateDto">report id and its new state</param>
    /// <param name="cancellationToken"></param>
    /// <returns>returns the updated report -- returns null if no report was found</returns>
    Task<Report?> UpdateState(ReportUpdateStateDto reportUpdateStateDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// save all changes in memory to DB
    /// </summary>
    /// <returns> True if changes lines are more than 0 </returns>
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default);
}
